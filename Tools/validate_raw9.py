#!/usr/bin/env python3
"""Read-only audit of retail RAW opcode 9 records.

Run: python Tools/validate_raw9.py path/to/original-game.zip
No original game data is extracted or redistributed.
"""
import argparse
from collections import Counter
import struct
import zipfile

def u16(data, offset):
    return struct.unpack_from("<H", data, offset)[0]

def scan_zip(path):
    found = {}
    inspected = 0
    with zipfile.ZipFile(path) as archive:
        for name in archive.namelist():
            if not name.upper().endswith(".RAW") or "/M/" not in name.upper():
                continue
            data = archive.read(name)
            if len(data) < 30:
                continue
            header = struct.unpack_from("<15H", data)
            start = header[0] + header[10]
            end = start + header[13]
            if header[13] == 0 or end > len(data) or start + 8 > end or u16(data, start) != 30003:
                continue
            inspected += 1
            count = u16(data, start + 6)
            cursor = start + u16(data, start + 4)
            for index in range(count):
                if cursor + 6 > end:
                    break
                size = u16(data, cursor)
                if size < 6 or size % 2 or cursor + size > end:
                    break
                if data[cursor + 3] == 9:
                    args = struct.unpack_from("<" + "H" * ((size - 6) // 2), data, cursor + 6)
                    found[(name.split("/")[-1].upper(), index, args)] = (name, args)
                cursor += size
    return inspected, found

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("retail_zip")
    args = ap.parse_args()
    inspected, found = scan_zip(args.retail_zip)
    print("RAW entries examined:", inspected)
    print("Unique opcode 9:", len(found))
    print("Argument lengths:", dict(Counter(len(v[1]) for v in found.values())))
    print("Sixth arg:", dict(Counter(v[1][5] if len(v[1]) > 5 else None for v in found.values())))
    print("Timeouts:", dict(sorted(Counter(v[1][4] for v in found.values() if len(v[1]) > 4).items())))
    print("X-axis:", sum(bool(v[1][0] & 64) for v in found.values()))
    print("Repeat:", sum(bool(v[1][0] & 32) for v in found.values()))
    print("Texture flags:", {i: sum(bool(v[1][0] & (1 << i)) for v in found.values()) for i in range(4)})
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
