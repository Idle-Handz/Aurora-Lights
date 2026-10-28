#!/usr/bin/env dotnet-script
// Clean-XamlCodeBehind.csx
// Run with: dotnet script Clean-XamlCodeBehind.csx -- <repo-root>
// Or just:  dotnet script Clean-XamlCodeBehind.csx
//
// If you don't have dotnet-script: dotnet tool install -g dotnet-script
// Alternatively compile and run as a normal console app.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

var root = Args.Count > 0 ? Args[0] : Directory.GetCurrentDirectory();
Console.WriteLine($"Scanning: {root}");

var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
int cleaned = 0;

foreach (var path in files)
{
    var original = File.ReadAllText(path);

    // Only process files that contain XAML boilerplate
    if (!original.Contains("_contentLoaded") &&
        !original.Contains("PresentationBuildTasks") &&
        !original.Contains("_CreateDelegate"))
        continue;

    var result = CleanFile(original);

    if (result != original)
    {
        File.WriteAllText(path, result, new UTF8Encoding(false));
        Console.WriteLine($"  Cleaned: {Path.GetFileName(path)}");
        cleaned++;
    }
}

Console.WriteLine($"\nDone. Cleaned {cleaned} files.");

static string CleanFile(string src)
{
    // Step 1: Remove single-line: private bool _contentLoaded;
    src = Regex.Replace(src, @"^[ \t]*private bool _contentLoaded;\s*\r?\n", "", RegexOptions.Multiline);

    // Step 2: Remove single-line internal field declarations (named XAML elements)
    // Pattern: internal <TypeName> <FieldName>;
    // Must be careful not to remove internal methods/properties
    src = Regex.Replace(src, @"^[ \t]*internal[ \t]+[\w.<>\[\], ]+[ \t]+\w+;\s*\r?\n", "", RegexOptions.Multiline);

    // Step 3: Remove IComponentConnector and IStyleConnector from interface lists
    // Handles both inline and multi-line class declarations
    src = Regex.Replace(src, @",\s*\r?\n?[ \t]*IComponentConnector\b", "", RegexOptions.Multiline);
    src = Regex.Replace(src, @",\s*\r?\n?[ \t]*IStyleConnector\b", "", RegexOptions.Multiline);

    // Step 4: Remove method blocks decorated with [GeneratedCode("PresentationBuildTasks"...)]
    // These come in groups: [DebuggerNonUserCode]\n[GeneratedCode(...)]\nmethodSignature { body }
    // We strip the entire block including attributes
    src = RemoveGeneratedBlocks(src);

    // Step 5: Remove bare IComponentConnector.Connect / IStyleConnector.Connect that have
    // [EditorBrowsable] attribute but no [GeneratedCode] (some decompiler variants)
    src = RemoveEditorBrowsableConnectBlocks(src);

    return src;
}

static string RemoveGeneratedBlocks(string src)
{
    // Match the attribute cluster + method signature + body
    // Targets: InitializeComponent, _CreateDelegate, IComponentConnector.Connect, IStyleConnector.Connect
    var pattern = new Regex(
        @"(?:[ \t]*\[(?:DebuggerNonUserCode|GeneratedCode|EditorBrowsable)[^\]]*\]\s*\r?\n)+[ \t]*(?:public void InitializeComponent|internal Delegate _CreateDelegate|void IComponentConnector\.Connect|void IStyleConnector\.Connect|public static void Main\b)[^\n]*\r?\n",
        RegexOptions.Multiline);

    var sb = new StringBuilder(src);
    bool changed = true;

    while (changed)
    {
        changed = false;
        var m = pattern.Match(sb.ToString());
        while (m.Success)
        {
            int start = m.Index;
            int bodyStart = m.Index + m.Length;
            string after = sb.ToString(bodyStart, sb.Length - bodyStart);

            // Handle two forms:
            // Form A: => expression;  (single line lambda body)
            // Form B: { ... }  (block body)
            var lambdaMatch = Regex.Match(after, @"^[ \t]*=>[^\n]*\n");
            if (lambdaMatch.Success)
            {
                sb.Remove(start, m.Length + lambdaMatch.Length);
                changed = true;
                break;
            }

            var braceMatch = Regex.Match(after, @"^[ \t]*\{");
            if (braceMatch.Success)
            {
                int end = FindClosingBrace(sb.ToString(), bodyStart + braceMatch.Index);
                if (end > 0)
                {
                    // Consume trailing newline if present
                    int removeLen = end - start + 1;
                    if (end + 1 < sb.Length && sb[end + 1] == '\r') removeLen++;
                    if (end + 1 < sb.Length && sb[end + 1] == '\n') removeLen++;
                    else if (end + 2 < sb.Length && sb[end + 1] == '\r' && sb[end + 2] == '\n') removeLen += 2;
                    sb.Remove(start, removeLen);
                    changed = true;
                    break;
                }
            }

            m = m.NextMatch();
        }
    }

    return sb.ToString();
}

static string RemoveEditorBrowsableConnectBlocks(string src)
{
    // Some files have [EditorBrowsable(EditorBrowsableState.Never)] without [GeneratedCode]
    var pattern = new Regex(
        @"[ \t]*\[EditorBrowsable\([^\]]*\)\]\s*\r?\n[ \t]*(?:void IComponentConnector\.Connect|void IStyleConnector\.Connect)[^\n]*\n",
        RegexOptions.Multiline);

    var sb = new StringBuilder(src);
    bool changed = true;

    while (changed)
    {
        changed = false;
        var m = pattern.Match(sb.ToString());
        while (m.Success)
        {
            int start = m.Index;
            int bodyStart = m.Index + m.Length;
            string after = sb.ToString(bodyStart, sb.Length - bodyStart);

            var lambdaMatch = Regex.Match(after, @"^[ \t]*=>[^\n]*\n");
            if (lambdaMatch.Success)
            {
                sb.Remove(start, m.Length + lambdaMatch.Length);
                changed = true;
                break;
            }

            var braceMatch = Regex.Match(after, @"^[ \t]*\{");
            if (braceMatch.Success)
            {
                int end = FindClosingBrace(sb.ToString(), bodyStart + braceMatch.Index);
                if (end > 0)
                {
                    int removeLen = end - start + 1;
                    if (end + 1 < sb.Length && (sb[end + 1] == '\n' || sb[end + 1] == '\r')) removeLen++;
                    sb.Remove(start, removeLen);
                    changed = true;
                    break;
                }
            }

            m = m.NextMatch();
        }
    }

    return sb.ToString();
}

static int FindClosingBrace(string src, int openBracePos)
{
    int depth = 0;
    bool inString = false;
    bool inChar = false;
    bool inLineComment = false;
    bool inBlockComment = false;

    for (int i = openBracePos; i < src.Length; i++)
    {
        char c = src[i];
        char next = i + 1 < src.Length ? src[i + 1] : '\0';

        if (inLineComment) { if (c == '\n') inLineComment = false; continue; }
        if (inBlockComment) { if (c == '*' && next == '/') { inBlockComment = false; i++; } continue; }
        if (inString) { if (c == '\\') { i++; continue; } if (c == '"') inString = false; continue; }
        if (inChar) { if (c == '\\') { i++; continue; } if (c == '\'') inChar = false; continue; }

        if (c == '/' && next == '/') { inLineComment = true; continue; }
        if (c == '/' && next == '*') { inBlockComment = true; continue; }
        if (c == '"') { inString = true; continue; }
        if (c == '\'') { inChar = true; continue; }

        if (c == '{') depth++;
        else if (c == '}')
        {
            depth--;
            if (depth == 0) return i;
        }
    }
    return -1;
}
