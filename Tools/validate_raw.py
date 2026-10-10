#!/usr/bin/env python3
"""Independent structural validator for Realms of the Haunting .RAW maps.
No game data is distributed with this repository.
"""
import argparse
import struct
from pathlib import Path


def validate(path: Path):
    data = path.read_bytes()
    if len(data) < 0x1E:
        raise ValueError("file too small")
    h = struct.unpack_from('<15H', data, 0)
    (vertices, version, sectors_off, faces_off, mappings_off, metadata_off,
     vertices_repeat, signature, mid_platforms, section7_size, vertices_size,
     objects_size, footer_size, commands_size, sector_count) = h
    if version != 0x70 or signature != 0x5257:
        raise ValueError(f"unexpected header version={version:#06x} signature={signature:#06x}")

    sector_offsets = {}
    sectors = []
    p = sectors_off
    for i in range(sector_count):
        sector_offsets[p] = i
        sectors.append(struct.unpack_from('<hhHHHBBbBHBBBBHHH', data, p))
        p += 0x1A

    face_count = struct.unpack_from('<H', data, faces_off - 2)[0]
    face_offsets, faces = {}, []
    p = faces_off
    for i in range(face_count):
        face_offsets[p] = i
        faces.append(struct.unpack_from('<6H', data, p))
        p += 0x0C

    mapping_count = struct.unpack_from('<H', data, mappings_off - 2)[0]
    mapping_offsets = {}
    p = mappings_off
    for i in range(mapping_count):
        mapping_offsets[p] = i
        entry = struct.unpack_from('<BBHHHH', data, p)
        p += 0x0A + (4 if entry[1] & 0x80 else 0)

    section_size, header_size, blank, vertex_count = struct.unpack_from('<4H', data, vertices)
    vertex_offsets = {}
    p = vertices + 8
    for i in range(vertex_count):
        vertex_offsets[p - vertices] = i
        p += 0x0C

    bad = dict(v1=0, v2=0, texture=0, sector=0, sister=0, first_face=0)
    for s in sectors:
        if s[9] not in face_offsets:
            bad['first_face'] += 1
    for a, b, tm, sec, sis, flags in faces:
        bad['v1'] += a not in vertex_offsets
        bad['v2'] += b not in vertex_offsets
        bad['texture'] += tm not in mapping_offsets
        bad['sector'] += sec not in sector_offsets
        bad['sister'] += sis not in (0, 0xFFFF) and sis not in face_offsets

    return {
        'file_size': len(data), 'version': version, 'signature': signature,
        'sectors': sector_count, 'faces': face_count, 'texture_mappings': mapping_count,
        'vertices': vertex_count, 'vertices_offset': vertices,
        'metadata_offset': metadata_off, 'mid_platforms_offset': mid_platforms,
        'command_section_size': commands_size, 'unresolved': bad,
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('raw', nargs='+', type=Path)
    args = ap.parse_args()
    failed = False
    for path in args.raw:
        try:
            r = validate(path)
            print(path)
            for k, v in r.items(): print(f"  {k}: {v}")
            if any(r['unresolved'].values()): failed = True
        except Exception as e:
            failed = True
            print(f"{path}: ERROR: {e}")
    raise SystemExit(1 if failed else 0)

if __name__ == '__main__':
    main()
