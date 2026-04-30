#if WINDOWS

using Aurora.PdfImport;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using PdfOcrWord = Aurora.PdfImport.OcrWord;
using PdfOcrPage = Aurora.PdfImport.OcrPage;

namespace Aurora.App.Services;

/// <summary>
/// Renders each page of a PDF to a bitmap using Windows.Data.Pdf and extracts
/// text with bounding boxes using Windows.Media.Ocr.
///
/// For page 1, a second targeted OCR pass crops the ability-score and HP box
/// regions that the full-page pass tends to miss due to overlapping graphics,
/// and merges the results into the word list before returning.
/// </summary>
internal static class WindowsPdfOcrService
{
    private const double RenderScale = 3.0;

    public static async Task<IReadOnlyList<PdfOcrPage>> RenderAndOcrAsync(string pdfPath)
    {
        var fileBytes = await File.ReadAllBytesAsync(pdfPath);
        using var raStream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(raStream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(fileBytes);
            await writer.StoreAsync();
        }
        raStream.Seek(0);

        var pdfDoc = await PdfDocument.LoadFromStreamAsync(raStream);

        var ocrEngine = OcrEngine.TryCreateFromUserProfileLanguages()
                     ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
        if (ocrEngine == null)
            throw new InvalidOperationException(
                "Windows OCR is not available. Ensure an English language pack is installed " +
                "(Settings → Time & Language → Language & Region → Add a language → English).");

        var pages = new List<PdfOcrPage>((int)pdfDoc.PageCount);

        // Page-0 bitmap is kept alive for the targeted second pass.
        SoftwareBitmap? page0Bitmap   = null;
        double page0WidthDip  = 0;
        double page0HeightDip = 0;

        for (uint i = 0; i < pdfDoc.PageCount; i++)
        {
            using var pdfPage = pdfDoc.GetPage(i);

            double pageWidthPt  = pdfPage.Size.Width;
            double pageHeightPt = pdfPage.Size.Height;
            uint   renderWidth  = (uint)(pageWidthPt * RenderScale);

            using var pngStream = new InMemoryRandomAccessStream();
            await pdfPage.RenderToStreamAsync(
                pngStream,
                new PdfPageRenderOptions { DestinationWidth = renderWidth });

            pngStream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(pngStream);

            // Page 0: keep the bitmap for the targeted second pass (disposed after).
            var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

            if (i == 0)
            {
                page0Bitmap    = bitmap;
                page0WidthDip  = pageWidthPt;
                page0HeightDip = pageHeightPt;
            }

            double pxToPt = 1.0 / RenderScale;
            var words = new List<PdfOcrWord>(
                (await ocrEngine.RecognizeAsync(bitmap)).Lines
                    .SelectMany(l => l.Words)
                    .Select(w =>
                    {
                        var r = w.BoundingRect;
                        return new PdfOcrWord(
                            w.Text,
                            Left:   r.X                  * pxToPt,
                            Top:    r.Y                  * pxToPt,
                            Right:  (r.X + r.Width)      * pxToPt,
                            Bottom: (r.Y + r.Height)     * pxToPt);
                    }));

            pages.Add(new PdfOcrPage(words, pageWidthPt, pageHeightPt));

            if (i != 0) bitmap.Dispose();
        }

        // ── Second pass: targeted crop OCR for page 0 ────────────────────────
        if (page0Bitmap != null)
        {
            using (page0Bitmap)
            {
                pages[0] = await AugmentPage0Async(
                    page0Bitmap, ocrEngine, pages[0], page0WidthDip, page0HeightDip);
            }
        }

        return pages;
    }

    // ── Targeted second-pass OCR ──────────────────────────────────────────────

    private static async Task<PdfOcrPage> AugmentPage0Async(
        SoftwareBitmap bitmap,
        OcrEngine ocrEngine,
        PdfOcrPage page0,
        double pageWidthDip,
        double pageHeightDip)
    {
        // OCR word coords were stored as pixel/RenderScale (see pxToPt = 1/RenderScale in the main loop),
        // so to get actual pixel coords we multiply by RenderScale, NOT bitmap.Width/pageWidth.
        double scaleX = RenderScale;
        double scaleY = RenderScale;

        var words = new List<PdfOcrWord>(page0.Words);

        bool strMissing = !words.Any(w =>
            w.Left < 160 &&
            string.Equals(w.Text, "STRENGTH", StringComparison.OrdinalIgnoreCase));
        bool dexMissing = !words.Any(w =>
            w.Left < 160 &&
            string.Equals(w.Text, "DEXTERITY", StringComparison.OrdinalIgnoreCase));
        bool hpMissing = !words.Any(w =>
            string.Equals(w.Text, "MAX",     StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.Text, "MAXIMUM", StringComparison.OrdinalIgnoreCase));

        if (!strMissing && !dexMissing && !hpMissing) return page0;

        // Derive the ability-box vertical spacing from two known labels.
        var conLabel = words.FirstOrDefault(w =>
            w.Left < 160 && string.Equals(w.Text, "CONSTITUTION", StringComparison.OrdinalIgnoreCase));
        var intLabel = words.FirstOrDefault(w =>
            w.Left < 160 && string.Equals(w.Text, "INTELLIGENCE", StringComparison.OrdinalIgnoreCase));

        // Measured spacing on the 2024 D&D Beyond sheet; used as a safe fallback.
        double boxSpacing = (conLabel != null && intLabel != null)
            ? intLabel.Top - conLabel.Top
            : 143.0;

        // Ability score box geometry (derived from measured spacing):
        //   cleanL: left trim that skips the decorative border element (43% of spacing)
        //   cleanR: right trim that skips the thin right border (170 - 15% of spacing)
        //   The crop includes the label (–2 DIP above box top) + number + small bottom margin.
        double cleanL    = boxSpacing * 0.43;
        double cleanR    = 170 - boxSpacing * 0.15;
        double innerTop  = boxSpacing * 0.06;   // small gap between top of box and number
        double cleanH    = boxSpacing * 0.40;   // height of the number region only

        if (strMissing && conLabel != null)
        {
            double top = conLabel.Top - 2 * boxSpacing;
            if (top > 0)
                words.AddRange(await OcrRegionAsync(
                    bitmap, ocrEngine, cleanL, top - 2, cleanR, top + innerTop + cleanH,
                    scaleX, scaleY, maskLeftBorder: true));
        }

        if (dexMissing && conLabel != null)
        {
            double top = conLabel.Top - boxSpacing;
            if (top > 0)
                words.AddRange(await OcrRegionAsync(
                    bitmap, ocrEngine, cleanL, top - 2, cleanR, top + innerTop + cleanH,
                    scaleX, scaleY, maskLeftBorder: true));
        }

        if (hpMissing)
        {
            // HP box sits in the centre column; use a broad region that covers the 2024 layout.
            words.AddRange(await OcrRegionAsync(
                bitmap, ocrEngine, 350, 100, 1100, 560, scaleX, scaleY));
        }

        return new PdfOcrPage(words, pageWidthDip, pageHeightDip);
    }

    /// <summary>
    /// Crops the bitmap to the specified DIP rectangle, scales it up, preprocesses it, and runs OCR.
    /// When <paramref name="maskLeftBorder"/> is true the left/right border columns are blanked to
    /// white (without binarizing) so the decorative frame in STR/DEX boxes doesn't confuse text
    /// detection.  Otherwise a binary threshold with auto-invert is applied (good for HP/other regions).
    /// Returns words with coordinates mapped back to DIP space.
    /// </summary>
    private static async Task<List<PdfOcrWord>> OcrRegionAsync(
        SoftwareBitmap source,
        OcrEngine ocrEngine,
        double dipLeft, double dipTop, double dipRight, double dipBottom,
        double scaleX, double scaleY,
        bool maskLeftBorder = false)
    {
        int pxLeft   = Math.Max(0, (int)(dipLeft   * scaleX));
        int pxTop    = Math.Max(0, (int)(dipTop    * scaleY));
        int pxRight  = Math.Min(source.PixelWidth,  (int)Math.Ceiling(dipRight  * scaleX));
        int pxBottom = Math.Min(source.PixelHeight, (int)Math.Ceiling(dipBottom * scaleY));
        int pxW = pxRight  - pxLeft;
        int pxH = pxBottom - pxTop;

        if (pxW <= 0 || pxH <= 0) return [];

        const int UpScale = 2;

        using var cropped   = await CropAndScaleAsync(source, pxLeft, pxTop, pxW, pxH, UpScale);
        using var processed = maskLeftBorder
            ? await MaskLeftBorderAsync(cropped)
            : await ThresholdAsync(cropped);

        var ocrResult = await ocrEngine.RecognizeAsync(processed);

        // Map cropped+scaled pixel coords back to DIP:
        //   dip_x = (crop_px_x / UpScale + pxLeft) / scaleX
        //         = crop_px_x * invX + dipLeft
        double invX = 1.0 / (scaleX * UpScale);
        double invY = 1.0 / (scaleY * UpScale);

        var result = new List<PdfOcrWord>();
        foreach (var line in ocrResult.Lines)
            foreach (var word in line.Words)
            {
                var r = word.BoundingRect;
                result.Add(new PdfOcrWord(
                    word.Text,
                    Left:   r.X              * invX + dipLeft,
                    Top:    r.Y              * invY + dipTop,
                    Right:  (r.X + r.Width)  * invX + dipLeft,
                    Bottom: (r.Y + r.Height) * invY + dipTop));
            }

        return result;
    }

    /// <summary>
    /// Crops then scales a region of a SoftwareBitmap.  Two separate encode/decode passes are used
    /// because combining Bounds + ScaledWidth/ScaledHeight in a single BitmapTransform causes
    /// ArgumentException on large (3427×4435) source bitmaps.
    /// </summary>
    private static async Task<SoftwareBitmap> CropAndScaleAsync(
        SoftwareBitmap source, int x, int y, int width, int height, int scale)
    {
        // Pass 1: crop only.
        using var stream = new InMemoryRandomAccessStream();
        var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        enc.SetSoftwareBitmap(source);
        await enc.FlushAsync();

        stream.Seek(0);
        var dec = await BitmapDecoder.CreateAsync(stream);
        var cropTransform = new BitmapTransform
        {
            Bounds = new BitmapBounds { X = (uint)x, Y = (uint)y, Width = (uint)width, Height = (uint)height },
        };
        using var cropped = await dec.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            cropTransform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        // Pass 2: scale only.
        using var scaleStream = new InMemoryRandomAccessStream();
        var scaleEnc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, scaleStream);
        scaleEnc.SetSoftwareBitmap(cropped);
        await scaleEnc.FlushAsync();

        scaleStream.Seek(0);
        var scaleDec = await BitmapDecoder.CreateAsync(scaleStream);
        var scaleTransform = new BitmapTransform
        {
            ScaledWidth       = (uint)(width  * scale),
            ScaledHeight      = (uint)(height * scale),
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        return await scaleDec.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            scaleTransform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);
    }

    /// <summary>
    /// Whites out the leftmost 90px and rightmost 30px of the 2× scaled crop so that decorative
    /// border elements (e.g. the shield frame in D&amp;D Beyond's STR/DEX boxes) are disconnected
    /// from the digit glyphs before OCR text detection runs.  No binarization is applied so digit
    /// shapes are preserved in their original colors.
    /// </summary>
    private static async Task<SoftwareBitmap> MaskLeftBorderAsync(SoftwareBitmap source)
    {
        using var readStream = new InMemoryRandomAccessStream();
        var readEnc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, readStream);
        readEnc.SetSoftwareBitmap(source);
        await readEnc.FlushAsync();

        readStream.Seek(0);
        var readDec   = await BitmapDecoder.CreateAsync(readStream);
        var pixelData = await readDec.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        byte[] bgra = pixelData.DetachPixelData();
        uint w = readDec.PixelWidth;
        uint h = readDec.PixelHeight;

        int maskLeft  = Math.Min(90,  (int)w);
        int maskRight = Math.Max(0,   (int)w - 30);
        for (int row = 0; row < h; row++)
            for (int col = 0; col < w; col++)
                if (col < maskLeft || col >= maskRight)
                {
                    int idx = (row * (int)w + col) * 4;
                    bgra[idx] = bgra[idx + 1] = bgra[idx + 2] = bgra[idx + 3] = 255;
                }

        using var writeStream = new InMemoryRandomAccessStream();
        var writeEnc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, writeStream);
        writeEnc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, w, h, 96.0, 96.0, bgra);
        await writeEnc.FlushAsync();

        writeStream.Seek(0);
        var writeDec = await BitmapDecoder.CreateAsync(writeStream);
        return await writeDec.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    /// <summary>
    /// Converts a Bgra8 bitmap to grayscale, applies a binary threshold, and returns a
    /// Bgra8 bitmap suitable for OCR.  Auto-inverts when the background is dark
    /// (i.e. light text on a dark box, common in D&D Beyond's ability score circles).
    /// </summary>
    private static async Task<SoftwareBitmap> ThresholdAsync(SoftwareBitmap source)
    {
        // Step 1: get pixel data as Bgra8 (universally supported by BitmapEncoder/Decoder),
        // then compute grayscale luminance manually — avoids Gray8 format support issues.
        using var readStream = new InMemoryRandomAccessStream();
        var readEnc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, readStream);
        readEnc.SetSoftwareBitmap(source);
        await readEnc.FlushAsync();

        readStream.Seek(0);
        var readDec   = await BitmapDecoder.CreateAsync(readStream);
        var pixelData = await readDec.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        byte[] bgra = pixelData.DetachPixelData(); // 4 bytes per pixel: B G R A
        uint w = readDec.PixelWidth;
        uint h = readDec.PixelHeight;

        byte[] gray = new byte[bgra.Length / 4];
        long sum = 0;
        for (int i = 0; i < gray.Length; i++)
        {
            byte b = bgra[i * 4], g = bgra[i * 4 + 1], r = bgra[i * 4 + 2];
            gray[i] = (byte)(0.299 * r + 0.587 * g + 0.114 * b);
            sum += gray[i];
        }

        // Auto-invert: dark background → light text → invert so OCR gets black-on-white.
        bool darkBackground = (double)sum / gray.Length < 96.0;

        for (int i = 0; i < gray.Length; i++)
        {
            byte bw = gray[i] >= 128 ? (byte)255 : (byte)0;
            gray[i] = darkBackground ? (byte)(255 - bw) : bw;
        }

        // Step 2: expand grayscale to Bgra8 (BitmapEncoder rejects Gray8), then decode.
        byte[] bgraOut = new byte[gray.Length * 4];
        for (int i = 0; i < gray.Length; i++)
        {
            bgraOut[i * 4]     = gray[i];
            bgraOut[i * 4 + 1] = gray[i];
            bgraOut[i * 4 + 2] = gray[i];
            bgraOut[i * 4 + 3] = 255;
        }

        using var writeStream = new InMemoryRandomAccessStream();
        var writeEnc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, writeStream);
        writeEnc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, w, h, 96.0, 96.0, bgraOut);
        await writeEnc.FlushAsync();

        writeStream.Seek(0);
        var writeDec = await BitmapDecoder.CreateAsync(writeStream);
        return await writeDec.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }
}

#endif
