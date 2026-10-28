"""Validate the actual signed Play artifact using the installed Android/JDK tools."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

ANDROID = "{http://schemas.android.com/apk/res/android}"
PAGE_SIZE = 16384


def run(*args):
    result = subprocess.run([str(arg) for arg in args], capture_output=True, text=True,
                            encoding="utf-8", errors="replace")
    if result.returncode:
        raise ValueError(f"{Path(args[0]).name} failed ({result.returncode}):\n"
                         f"{result.stdout}\n{result.stderr}")
    return result.stdout


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_elf(name, data):
    require(data[:6] == b"\x7fELF\x02\x01", f"{name}: expected little-endian ELF64")
    require(struct.unpack_from("<H", data, 18)[0] == 183, f"{name}: expected ARM64")
    phoff = struct.unpack_from("<Q", data, 32)[0]
    phsize, phcount = struct.unpack_from("<HH", data, 54)
    require(phsize >= 56, f"{name}: invalid ELF program header size")
    headers = [struct.unpack_from("<IIQQQQQQ", data, phoff + index * phsize)
               for index in range(phcount)]
    load_count = 0
    for kind, _, offset, vaddr, _, _, memsize, alignment in headers:
        if kind == 1:  # PT_LOAD
            load_count += 1
            require(alignment >= PAGE_SIZE and (vaddr - offset) % PAGE_SIZE == 0,
                    f"{name}: LOAD segment is not 16 KB aligned (alignment={alignment})")
        if kind == 0x6474E552 and memsize:  # PT_GNU_RELRO
            # Bionic rounds RELRO protection outward to whole pages. Padding is
            # safe, but the rounded range must not make other writable data RO.
            end = vaddr + memsize
            extra_ranges = ((vaddr // PAGE_SIZE * PAGE_SIZE, vaddr),
                            (end, (end + PAGE_SIZE - 1) // PAGE_SIZE * PAGE_SIZE))
            for load_kind, flags, _, start, _, _, size, _ in headers:
                if load_kind == 1 and flags & 2:  # Writable PT_LOAD
                    for extra_start, extra_end in extra_ranges:
                        require(max(start, extra_start) >= min(start + size, extra_end),
                                f"{name}: 16 KB RELRO protection overlaps writable data")
    require(load_count > 0, f"{name}: no LOAD segments")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bundle", type=Path)
    parser.add_argument("--java", required=True, type=Path)
    parser.add_argument("--bundletool", required=True, type=Path)
    parser.add_argument("--android-sdk", required=True, type=Path)
    parser.add_argument("--keystore", required=True, type=Path)
    parser.add_argument("--alias", required=True)
    parser.add_argument("--store-pass-file", required=True, type=Path)
    parser.add_argument("--key-pass-file", required=True, type=Path)
    parser.add_argument("--version-code", required=True, type=int)
    parser.add_argument("--version-name", required=True)
    args = parser.parse_args()
    bundle = args.bundle.resolve()
    output = bundle.parent
    (output / "play-validation.json").unlink(missing_ok=True)
    suffix = ".exe" if os.name == "nt" else ""
    java = [args.java, "-jar", args.bundletool]

    # Trust only the supplied upload certificate. -strict also rejects unsigned
    # payload entries and a signature from a different key/alias.
    signing = run(args.java.parent / f"jarsigner{suffix}", "-J-Duser.language=en",
                  "-verify", "-strict", "-keystore", args.keystore,
                  "-storepass:file", args.store_pass_file, bundle, args.alias)
    require("jar verified" in signing.lower(), "The AAB is unsigned or its signature was not verified")
    run(*java, "validate", f"--bundle={bundle}")
    manifest_text = run(*java, "dump", "manifest", f"--bundle={bundle}", "--module=base")
    config_text = run(*java, "dump", "config", f"--bundle={bundle}")
    (output / "play-manifest.xml").write_text(manifest_text, encoding="utf-8")
    (output / "play-bundle-config.json").write_text(config_text, encoding="utf-8")
    manifest = ET.fromstring(manifest_text)
    sdk = manifest.find("uses-sdk")
    app = manifest.find("application")
    require(sdk is not None and app is not None, "Missing SDK or application manifest declaration")
    require(manifest.get("package") == "com.auroralights.app", "Unexpected application ID")
    require(manifest.get(ANDROID + "versionCode") == str(args.version_code), "Wrong versionCode")
    require(1 < args.version_code <= 2100000000, "versionCode outside Play's supported range")
    require(manifest.get(ANDROID + "versionName") == args.version_name, "Wrong versionName")
    require(int(sdk.get(ANDROID + "targetSdkVersion", "0")) >= 36, "Google Play requires target API 36+")
    require(int(sdk.get(ANDROID + "minSdkVersion", "0")) == 24, "Unexpected minimum Android version")
    for flag in ("debuggable", "testOnly"):
        require(app.get(ANDROID + flag, "false") == "false", f"Release must not be {flag}")
    permissions = [p.get(ANDROID + "name") for p in manifest.findall("uses-permission")]
    require("android.permission.REQUEST_INSTALL_PACKAGES" not in permissions,
            "Play distribution must not request APK installation permission")
    config = json.loads(config_text)
    native_config = config.get("optimizations", {}).get("uncompressNativeLibraries", {})
    require(native_config.get("alignment") == "PAGE_ALIGNMENT_16K",
            "The bundle must request 16 KB native-library ZIP alignment")

    with zipfile.ZipFile(bundle) as archive:
        libraries = [n for n in archive.namelist() if re.match(r"base/lib/[^/]+/[^/]+\.so$", n)]
        abis = sorted({n.split("/")[2] for n in libraries})
        require(abis == ["arm64-v8a"], f"Expected ARM64 phone bundle, found {abis}")
        for name in libraries:
            validate_elf(name, archive.read(name))
        require(not any("google-drive-client.json" in n.lower() for n in archive.namelist()),
                "Desktop OAuth configuration must not be packaged as an Android asset")

    certificate = run(args.java.parent / f"keytool{suffix}", "-J-Duser.language=en",
                      "-list", "-v", "-keystore", args.keystore, "-alias", args.alias,
                      "-storepass:file", args.store_pass_file)
    sha256_match = re.search(r"SHA256:\s*([0-9A-F:]+)", certificate)
    sha1_match = re.search(r"SHA1:\s*([0-9A-F:]+)", certificate)
    require(sha256_match is not None and sha1_match is not None, "Could not read upload certificate fingerprints")

    # Check the APKs Play-style delivery generates, not just the AAB ZIP. Keep the
    # set for optional adb/bundletool testing; it is not the file uploaded to Play.
    versions = [p for p in (args.android_sdk / "build-tools").iterdir()
                if re.fullmatch(r"\d+\.\d+\.\d+", p.name)]
    require(versions, "No stable Android SDK build-tools installation found")
    build_tools = max(versions, key=lambda p: tuple(map(int, p.name.split("."))))
    apk_set = output / "play-device-test.apks"
    run(*java, "build-apks", f"--bundle={bundle}", f"--output={apk_set}", "--overwrite",
        f"--aapt2={build_tools / ('aapt2' + suffix)}",
        f"--ks={args.keystore}", f"--ks-key-alias={args.alias}",
        f"--ks-pass=file:{args.store_pass_file}", f"--key-pass=file:{args.key_pass_file}")
    zipalign = build_tools / f"zipalign{suffix}"
    apksigner = build_tools / "lib" / "apksigner.jar"
    apk_names = []
    with zipfile.ZipFile(apk_set) as archive:
        for name in archive.namelist():
            if not name.endswith(".apk"):
                continue
            # Flatten entries into a controlled scratch directory (no archive paths).
            apk_path = output / "verified-apks" / Path(name).name
            apk_path.parent.mkdir(exist_ok=True)
            apk_path.write_bytes(archive.read(name))
            run(zipalign, "-c", "-P", "16", "4", apk_path)
            signature = run(args.java, "-jar", apksigner, "verify", "--print-certs", apk_path)
            expected_sha256 = sha256_match[1].replace(":", "").lower()
            require(expected_sha256 in signature.lower(), f"{name}: unexpected APK signing certificate")
            apk_names.append(name)
    require(apk_names, "bundletool did not generate installable APKs")

    report = {
        "bundle": bundle.name,
        "sha256": hashlib.sha256(bundle.read_bytes()).hexdigest(),
        "package": manifest.get("package"),
        "versionCode": args.version_code,
        "versionName": args.version_name,
        "targetSdk": int(sdk.get(ANDROID + "targetSdkVersion")),
        "minSdk": int(sdk.get(ANDROID + "minSdkVersion")),
        "abis": abis,
        "uploadCertificateSha1": sha1_match[1],
        "uploadCertificateSha256": sha256_match[1],
        "permissions": permissions,
        "nativeLibrariesChecked": len(libraries),
        "nativeElfAlignment": "16 KB LOAD alignment and RELRO/writable-data separation passed",
        "generatedApksChecked": len(apk_names),
        "generatedApkChecks": "16 KB ZIP alignment and signatures passed",
        "runtimeTesting": "Not performed by this packaging validator",
        "googlePlaySigning": "Register the Play app-signing certificate for Android OAuth before Drive testing"
    }
    (output / "play-validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, struct.error, zipfile.BadZipFile, ET.ParseError) as error:
        print(f"Bundle validation failed: {error}", file=sys.stderr)
        sys.exit(1)
