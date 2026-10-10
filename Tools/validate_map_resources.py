#!/usr/bin/env python3
"""Choose the most likely primary DEMO*.DAS for a ROTH RAW.

The scoring mirrors RothMapResourceResolver.cs: sector, intermediate-platform,
face texture and source-0/source-1 object references are compared against valid
FAT entries in DEMO.DAS..DEMO4.DAS. Palette/color encodings (>= 0x8000) are
not treated as FAT indices.
"""
import argparse
import struct
from pathlib import Path


def das_indices(path):
    b = Path(path).read_bytes()
    if b[:4] != b'DASP':
        return set()
    fat = struct.unpack_from('<I', b, 8)[0]
    counts = struct.unpack_from('<4H', b, 0x30)
    out = set()
    for i in range(sum(counts)):
        off, size, f1, f2 = struct.unpack_from('<IHBB', b, fat + i * 8)
        if size or (f1 & 0x20):
            out.add(i)
    return out


def raw_refs(path):
    d = Path(path).read_bytes()
    h = struct.unpack_from('<15H', d, 0)
    vertices, sectors, faces, mappings, mid, sector_count = h[0], h[2], h[3], h[4], h[8], h[14]
    vertices_section_size, command_size, section7_size = h[10], h[13], h[9]
    refs = set()

    def add(v):
        if 0 <= v < 0x8000 and v != 0x7fff:
            refs.add(v)

    # Sectors.
    for i in range(sector_count):
        p = sectors + i * 0x1a
        add(struct.unpack_from('<H', d, p + 6)[0])
        add(struct.unpack_from('<H', d, p + 8)[0])

    # Face texture mappings.
    mapping_count = struct.unpack_from('<H', d, mappings - 2)[0]
    p = mappings
    for _ in range(mapping_count):
        typ = d[p + 1]
        add(struct.unpack_from('<H', d, p + 2)[0])
        add(struct.unpack_from('<H', d, p + 4)[0])
        add(struct.unpack_from('<H', d, p + 6)[0])
        p += 10 + (4 if typ & 0x80 else 0)

    # Intermediate platforms. Count word immediately precedes the section.
    if mid and mid >= 2 and mid <= len(d):
        platform_count = struct.unpack_from('<H', d, mid - 2)[0]
        p = mid
        for _ in range(platform_count):
            if p + 14 > len(d):
                break
            add(struct.unpack_from('<H', d, p + 0)[0])
            add(struct.unpack_from('<H', d, p + 6)[0])
            p += 14

    # Object section follows vertices + commands + section7.
    object_start = vertices + vertices_section_size + command_size + section7_size
    if object_start + 2 <= len(d):
        section_size = struct.unpack_from('<H', d, object_start)[0]
        section_end = object_start + section_size
        table_start = object_start + 2
        if section_size >= 2 + sector_count * 2 and section_end <= len(d):
            offsets = struct.unpack_from('<' + 'H' * sector_count, d, table_start)
            for rel in offsets:
                if not rel:
                    continue
                p = object_start + rel
                if p + 2 > section_end:
                    continue
                count = d[p]
                p += 2
                for _ in range(count):
                    if p + 16 > section_end:
                        break
                    texture_index = d[p + 4]
                    texture_source = d[p + 5]
                    if texture_source == 0:
                        add(texture_index + 4096)
                    elif texture_source == 1:
                        add(texture_index + 4352)
                    p += 16

    return refs


def find(root, name):
    for rel in (Path('DATA/M') / name, Path('ROTH/M') / name, Path('M') / name, Path(name)):
        p = Path(root) / rel
        if p.is_file():
            return p
    return None


def main():
    ap = argparse.ArgumentParser(description='Choose the best DEMO*.DAS for a ROTH RAW by FAT coverage.')
    ap.add_argument('root')
    ap.add_argument('raw')
    a = ap.parse_args()
    raw = Path(a.raw)
    if not raw.is_file():
        raw = find(a.root, a.raw if a.raw.lower().endswith('.raw') else a.raw + '.RAW')
    if not raw:
        raise SystemExit('RAW not found')

    refs = raw_refs(raw)
    rows = []
    for i in range(5):
        name = 'DEMO.DAS' if i == 0 else f'DEMO{i}.DAS'
        p = find(a.root, name)
        if not p:
            continue
        valid = das_indices(p)
        n = len(refs & valid)
        # C# resolver keeps the first candidate on exact coverage/resolved ties.
        rows.append((n / len(refs) if refs else 1.0, n, name, p))

    rows.sort(key=lambda r: (-r[0], -r[1], ['DEMO.DAS','DEMO1.DAS','DEMO2.DAS','DEMO3.DAS','DEMO4.DAS'].index(r[2])))
    print('RAW:', raw, 'refs=', len(refs))
    for cov, n, name, p in rows:
        print(f'{name:9} {n:4}/{len(refs):4} {cov:7.2%}  {p}')
    if rows:
        print('BEST:', rows[0][2])


if __name__ == '__main__':
    main()
