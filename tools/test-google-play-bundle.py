"""Regression checks for ELF compatibility validation; no Android SDK needed."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location(
    "play_bundle", Path(__file__).with_name("verify-google-play-bundle.py"))
validator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(validator)


def elf(*headers):
    data = bytearray(64 + len(headers) * 56)
    data[:6] = b"\x7fELF\x02\x01"
    struct.pack_into("<H", data, 18, 183)
    struct.pack_into("<Q", data, 32, 64)
    struct.pack_into("<HH", data, 54, 56, len(headers))
    for index, header in enumerate(headers):
        struct.pack_into("<IIQQQQQQ", data, 64 + index * 56, *header)
    return data


def load(start, size, alignment=16384):
    return (1, 6, start, start, start, size, size, alignment)


def relro(start, size):
    return (0x6474E552, 4, start, start, start, size, size, 1)


class ElfCompatibilityTests(unittest.TestCase):
    def test_accepts_relro_with_gap_before_next_writable_segment(self):
        validator.validate_elf("padded.so", elf(
            load(0x4000, 0x1000), relro(0x4000, 0x1000), load(0x8000, 0x1000)))

    def test_rejects_writable_data_after_relro_in_same_page(self):
        with self.assertRaisesRegex(ValueError, "overlaps writable"):
            validator.validate_elf("unsafe.so", elf(
                load(0x4000, 0x2000), relro(0x4000, 0x1000)))

    def test_rejects_writable_data_before_relro_in_same_page(self):
        with self.assertRaisesRegex(ValueError, "overlaps writable"):
            validator.validate_elf("unsafe.so", elf(
                load(0x4000, 0x4000), relro(0x5000, 0x3000)))

    def test_rejects_4k_load_alignment(self):
        with self.assertRaisesRegex(ValueError, "LOAD segment"):
            validator.validate_elf("4k.so", elf(load(0, 0x1000, alignment=4096)))

    def test_rejects_non_arm64_binary(self):
        data = elf(load(0, 0x1000))
        struct.pack_into("<H", data, 18, 62)
        with self.assertRaisesRegex(ValueError, "expected ARM64"):
            validator.validate_elf("x64.so", data)


if __name__ == "__main__":
    unittest.main()
