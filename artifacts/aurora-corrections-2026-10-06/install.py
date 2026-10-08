#!/usr/bin/env python3
"""Install reviewed Aurora corrections. Dry-run by default; Python standard library only."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import sys
import tempfile
import xml.etree.ElementTree as ET


NAMESPACE = "urn:aurora-lights:corrections:1"
SECTION = "{" + NAMESPACE + "}corrections"
BASELINE = "{" + NAMESPACE + "}baseline"


class ValidationError(Exception):
    pass


class NoDtdBuilder(ET.TreeBuilder):
    def doctype(self, name, pubid, system):
        raise ValidationError("XML document types and entity declarations are not allowed")


def xml_root(data, label):
    try:
        root = ET.fromstring(data, parser=ET.XMLParser(target=NoDtdBuilder()))
    except (ET.ParseError, ValidationError) as exc:
        raise ValidationError("{}: {}".format(label, exc)) from exc
    if root.tag != "elements":
        raise ValidationError("{}: expected an unnamespaced <elements> root".format(label))
    return root


def xml_shape(element):
    # Attribute order and namespace declaration placement do not change XML meaning.
    # Keep text and whitespace: a false mismatch is safer than discarding a local edit.
    return (element.tag, tuple(sorted(element.attrib.items())), element.text,
            tuple(xml_shape(child) for child in element), element.tail)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def checked_hash(value, label):
    if not isinstance(value, str) or not re.fullmatch(r"[0-9a-fA-F]{64}", value):
        raise ValidationError("{}: invalid SHA-256 value".format(label))
    return value.lower()


def is_link(path):
    try:
        info = path.lstat()
    except FileNotFoundError:
        return False
    # On Windows this also rejects junctions and other reparse-point directories.
    return stat.S_ISLNK(info.st_mode) or bool(
        getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400))


def check_no_links(path):
    for part in reversed((path,) + tuple(path.parents)):
        if is_link(part):
            raise ValidationError("Symbolic links/junctions are not allowed: {}".format(part))


def checked_root(path):
    absolute = Path(os.path.abspath(os.path.expanduser(str(path))))
    check_no_links(absolute)
    if not absolute.is_dir():
        raise ValidationError("Content folder does not exist: {}".format(absolute))
    return absolute.resolve(strict=True)


def relative_parts(value):
    if not isinstance(value, str) or not value or "\\" in value or ":" in value or "\x00" in value:
        raise ValidationError("Invalid portable relative path: {!r}".format(value))
    parts = value.split("/")
    if PurePosixPath(value).is_absolute() or any(part in ("", ".", "..") for part in parts):
        raise ValidationError("Unsafe relative path: {!r}".format(value))
    return parts


def contained(root, relative):
    path = root.joinpath(*relative_parts(relative))
    check_no_links(path)
    try:
        path.resolve(strict=False).relative_to(root)
    except ValueError as exc:
        raise ValidationError("Path escapes its root: {}".format(relative)) from exc
    return path


def source_key(root, relative):
    if relative_parts(relative)[0].casefold() == "user" or not relative.lower().endswith(".xml"):
        raise ValidationError("Correction source must be an XML file outside user/: {}".format(relative))
    return os.path.normcase(str(contained(root, relative)))


def correction_source(root, label, required=False):
    sections = [child for child in root if child.tag.split("}")[-1] == "corrections"]
    if not sections:
        if required or any(str(child.tag).startswith("{urn:aurora-lights:corrections:") for child in root.iter()):
            raise ValidationError("{}: missing supported correction metadata".format(label))
        return None
    if len(sections) != 1 or sections[0].tag != SECTION or sections[0].get("version") != "1":
        raise ValidationError("{}: ambiguous or unsupported correction metadata".format(label))
    section = sections[0]
    baselines = section.findall(BASELINE)
    if len(baselines) != 1 or baselines[0].get("encoding") != "escaped-xml" or len(baselines[0]):
        raise ValidationError("{}: invalid correction baseline".format(label))
    xml_root(baselines[0].text or "", str(label) + " baseline")
    source = section.get("source-path")
    relative_parts(source)
    return source


def managed_files(content_root):
    """Return all managed files by source, and a snapshot for the pre-write recheck."""
    local = contained(content_root, "user/local")
    if not local.exists():
        return {}, {}
    if not local.is_dir():
        raise ValidationError("Expected a directory: {}".format(local))
    by_source, snapshot = {}, {}
    for directory, dirs, files in os.walk(local, followlinks=False):
        dirs.sort()
        for name in dirs + files:
            if is_link(Path(directory) / name):
                raise ValidationError("Symbolic links/junctions are not allowed: {}".format(Path(directory) / name))
        for name in sorted(files):
            if not name.lower().endswith(".xml"):
                continue
            path = Path(directory) / name
            data = path.read_bytes()
            snapshot[str(path)] = digest(data)
            parsed = xml_root(data, path)
            source = correction_source(parsed, path)
            if source is not None:
                key = source_key(content_root, source)
                by_source.setdefault(key, []).append((path, xml_shape(parsed)))
    return by_source, snapshot


def load_manifest(bundle):
    manifest_path = contained(bundle, "manifest.json")
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except (ValueError, OSError) as exc:
        raise ValidationError("Cannot read manifest.json: {}".format(exc)) from exc
    if not isinstance(manifest, dict) or not manifest.get("version") or not isinstance(manifest.get("entries"), list):
        raise ValidationError("Manifest must contain a version and an entries list")
    return manifest


def make_plan(bundle, content_root, manifest, include_personal):
    managed, snapshot = managed_files(content_root)
    plan, messages, errors = [], [], []
    seen_ids, seen_destinations, selected_sources = set(), set(), set()
    for entry in manifest["entries"]:
        label = entry.get("id", "<unnamed>") if isinstance(entry, dict) else "<invalid entry>"
        try:
            if not isinstance(entry, dict) or not isinstance(label, str) or not label or label in seen_ids:
                raise ValidationError("Missing or duplicate entry id")
            seen_ids.add(label)
            category = entry.get("category")
            if category not in ("repair", "personal-preference"):
                raise ValidationError("Unknown category")
            destination = entry.get("destination")
            parts = relative_parts(destination)
            if parts[:2] != ["user", "local"] or len(parts) < 3 or not destination.lower().endswith(".xml"):
                raise ValidationError("Destination must be an XML file under user/local/")
            target = contained(content_root, destination)
            target_key = os.path.normcase(str(target))
            if target_key in seen_destinations:
                raise ValidationError("Multiple entries target the same destination")
            seen_destinations.add(target_key)
            if category == "personal-preference" and not include_personal:
                messages.append("SKIP {}: personal preference (opt in with --include-personal-preferences)".format(label))
                continue
            variants = entry.get("variants")
            if not isinstance(variants, list) or not variants:
                raise ValidationError("No source variants supplied")
            available, seen_variants = [], set()
            for variant in variants:
                if not isinstance(variant, dict):
                    raise ValidationError("Invalid variant")
                source = variant.get("sourcePath")
                key = source_key(content_root, source)
                if key in seen_variants:
                    raise ValidationError("Duplicate source path among variants")
                seen_variants.add(key)
                path = contained(content_root, source)
                checked_hash(variant.get("sourceSha256"), "source")
                checked_hash(variant.get("templateSha256"), "template")
                contained(bundle, variant.get("template"))
                if path.exists():
                    if not path.is_file():
                        raise ValidationError("Source is not a file: {}".format(path))
                    available.append((variant, path, key))
            if not available:
                messages.append("SKIP {}: its source is not installed".format(label))
                continue
            if len(available) != 1:
                raise ValidationError("Ambiguous installed sources: {}".format(", ".join(str(item[1]) for item in available)))
            variant, source_path, key = available[0]
            if key in selected_sources:
                raise ValidationError("Multiple entries manage the same installed source")
            selected_sources.add(key)
            source_hash = checked_hash(variant["sourceSha256"], "source")
            source_data = source_path.read_bytes()
            if digest(source_data) != source_hash:
                raise ValidationError("Source hash differs from the reviewed version: {}".format(source_path))
            xml_root(source_data, source_path)
            template_path = contained(bundle, variant["template"])
            template_data = template_path.read_bytes()
            template_hash = checked_hash(variant["templateSha256"], "template")
            if digest(template_data) != template_hash:
                raise ValidationError("Template hash mismatch: {}".format(template_path))
            template_xml = xml_root(template_data, template_path)
            if correction_source(template_xml, template_path, required=True) != variant["sourcePath"]:
                raise ValidationError("Template correction source-path does not match its variant")
            existing = managed.get(key, [])
            if len(existing) > 1:
                raise ValidationError("Multiple existing managed overrides require consolidation: {}".format(
                    ", ".join(str(item[0]) for item in existing)))
            if existing:
                path, shape = existing[0]
                if shape == xml_shape(template_xml):
                    messages.append("ALREADY MANAGED {}: {}".format(label, path))
                else:
                    messages.append("SKIP {}: existing override {}; manual merge required; nothing overwritten".format(label, path))
                continue
            if target.exists():
                raise ValidationError("Destination already exists; nothing will be overwritten: {}".format(target))
            plan.append({"id": label, "destination": destination, "target": target,
                         "source": variant["sourcePath"], "source_hash": source_hash,
                         "data": template_data, "hash": template_hash})
            messages.append("INSTALL {}: {}".format(label, target))
        except (ValidationError, OSError, KeyError, TypeError) as exc:
            errors.append("{}: {}".format(label, exc))
    return plan, messages, errors, snapshot


def apply_plan(content_root, plan, snapshot):
    # Validate the complete plan again before creating any directory or file.
    _, current_snapshot = managed_files(content_root)
    if snapshot != current_snapshot:
        raise ValidationError("Local XML changed during planning; rerun the installer")
    for item in plan:
        if contained(content_root, item["destination"]).exists():
            raise ValidationError("Destination appeared during planning: {}".format(item["target"]))
        if digest(contained(content_root, item["source"]).read_bytes()) != item["source_hash"]:
            raise ValidationError("Source changed during planning: {}".format(item["source"]))

    staged, installed = [], []
    try:
        for item in plan:
            target = contained(content_root, item["destination"])
            target.parent.mkdir(parents=True, exist_ok=True)
            check_no_links(target.parent)
            fd, temporary = tempfile.mkstemp(prefix="." + target.name + ".", suffix=".tmp", dir=str(target.parent))
            staged.append((item, Path(temporary)))
            with os.fdopen(fd, "wb") as stream:
                stream.write(item["data"])
                stream.flush()
                os.fsync(stream.fileno())
        for item, temporary in staged:
            target = contained(content_root, item["destination"])
            # A hard link publishes the complete file atomically and refuses an existing name.
            # Do not fall back to a clobbering rename or a partially visible copy.
            os.link(str(temporary), str(target))
            installed.append(item)
    except BaseException:
        for item in reversed(installed):
            try:
                target = contained(content_root, item["destination"])
                if target.is_file() and digest(target.read_bytes()) == item["hash"]:
                    target.unlink()
                else:
                    print("ROLLBACK NEEDS REVIEW: {} changed; left untouched".format(target), file=sys.stderr)
            except (OSError, ValidationError) as exc:
                print("ROLLBACK NEEDS REVIEW: {}: {}".format(item["target"], exc), file=sys.stderr)
        raise
    finally:
        for _, temporary in staged:
            try:
                temporary.unlink()
            except FileNotFoundError:
                pass


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("content_root", help="Existing Aurora custom content folder")
    parser.add_argument("--apply", action="store_true", help="Install the validated plan; default is read-only dry-run")
    parser.add_argument("--include-personal-preferences", action="store_true", help="Include optional personal changes")
    args = parser.parse_args(argv)
    try:
        bundle = checked_root(Path(__file__).absolute().parent)
        content_root = checked_root(args.content_root)
        manifest = load_manifest(bundle)
        plan, messages, errors, snapshot = make_plan(bundle, content_root, manifest, args.include_personal_preferences)
        for message in messages:
            print(message)
        if errors:
            for error in errors:
                print("ERROR: " + error, file=sys.stderr)
            print("Validation failed. No files were installed.", file=sys.stderr)
            return 1
        if not args.apply:
            print("DRY RUN: {} file(s) would be installed. No files changed. Use --apply to install.".format(len(plan)))
            return 0
        apply_plan(content_root, plan, snapshot)
        print("Installed {} file(s). Refresh the content database in Aurora to activate them.".format(len(plan)))
        return 0
    except (ValidationError, OSError, ValueError) as exc:
        print("ERROR: {}".format(exc), file=sys.stderr)
        print("Installation stopped. Existing files were not overwritten. Review any rollback notice above.", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
