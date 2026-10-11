#!/usr/bin/env python3
"""Read-only RAW9 portal/shared-vertex topology audit for legally owned maps.

This is a preflight diagnostic, never a movement-safety certification. It
intentionally does NOT change original maps or reproduce runtime collision.
"""
import argparse
import json
from pathlib import Path
import struct
import zipfile
from audit_roth_raw9_geometry import audit_raw, u16, s16


def topology_for_map(data):
    base = audit_raw(data)
    header = struct.unpack_from('<15H', data)
    vertex_base, sector_base, face_base = header[0], header[2], header[3]
    vertex_count = u16(data, vertex_base + 6)
    face_count = u16(data, face_base - 2)
    sector_count = header[14]
    if face_base + face_count * 12 > len(data):
        raise ValueError('face table extends outside file')
    if sector_base + sector_count * 26 > len(data):
        raise ValueError('sector table extends outside file')
    sectors = {u16(data, sector_base + i * 26 + 20): sector_base + i * 26
               for i in range(sector_count)}

    def vertex_index(raw_offset):
        if raw_offset < 8 or (raw_offset - 8) % 12:
            return None
        index = (raw_offset - 8) // 12
        return index if index < vertex_count else None

    def xy(index):
        address = vertex_base + 8 + index * 12
        return (s16(data, address + 8), s16(data, address + 10))

    faces = []
    for index in range(face_count):
        offset = face_base + index * 12
        faces.append(dict(index=index, offset=offset,
                          first=vertex_index(u16(data, offset)),
                          second=vertex_index(u16(data, offset + 2)),
                          owner=u16(data, offset + 6),
                          sister=u16(data, offset + 8)))

    for command in base['commands']:
        if 'error' in command:
            command['topology'] = {'status': 'invalid_reference', 'manual_review_required': True}
            continue
        selected = set(command['vertices_sorted'])
        current = sectors[command['sector_id']]
        external_references = sorted({f['index'] for f in faces
                                     if f['owner'] != current and
                                     (f['first'] in selected or f['second'] in selected)})
        original_coords = {xy(i) for i in selected}
        external_coincident = sorted({f['index'] for f in faces
                                     if f['owner'] != current and
                                     any(v is not None and v not in selected and
                                         xy(v) in original_coords
                                         for v in (f['first'], f['second']))})
        sisters = []
        malformed_sisters = []
        for f in faces:
            if f['owner'] != current or f['sister'] in (0, 0xffff):
                continue
            target = f['sister']
            if target < face_base or (target-face_base) % 12 != 0 or target >= face_base + 12*face_count:
                malformed_sisters.append(f['index'])
                continue
            other = faces[(target-face_base)//12]
            if other['owner'] == current:
                continue
            sisters.append({'face_index': f['index'], 'sister_face_index': other['index'],
                            'moving_endpoint_count': sum(v in selected for v in (f['first'], f['second']))})
        linked_moving = [p for p in sisters if p['moving_endpoint_count']]
        hazards = []
        if external_references:
            hazards.append('selected_vertex_referenced_by_other_sector')
        if linked_moving:
            hazards.append('cross_sector_sister_face_touches_selected_vertex')
        if external_coincident:
            hazards.append('external_distinct_vertex_at_selected_coordinates')
        if malformed_sisters:
            hazards.append('invalid_sister_face_pointer')
        if command.get('non_quad_sector'):
            hazards.append('sector_has_more_than_four_faces')
        command['topology'] = {
            'status': 'review' if hazards else 'no_static_hazard_detected',
            'manual_review_required': True,
            'hazards': hazards,
            'external_vertex_reference_faces': external_references,
            'external_coincident_faces': external_coincident,
            'cross_sector_sister_edges': sisters,
            'invalid_sister_face_indices': malformed_sisters,
        }
    base['topology_review_count'] = sum(
        command.get('topology', {}).get('status') != 'no_static_hazard_detected'
        for command in base['commands'])
    base['notice'] = 'Static preflight only: absence of hazards is not proof of safe geometry, portals or collisions.'
    return base


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('source', type=Path, help='Owned .RAW file, folder or ZIP containing .RAW')
    ap.add_argument('--json', type=Path)
    args = ap.parse_args(argv)
    results = []
    try:
        if args.source.is_dir():
            files = [(str(p), p.read_bytes()) for p in sorted(args.source.rglob('*')) if p.suffix.lower() == '.raw']
        elif zipfile.is_zipfile(args.source):
            with zipfile.ZipFile(args.source) as archive:
                files = [(name, archive.read(name)) for name in archive.namelist() if name.lower().endswith('.raw')]
        else:
            files = [(str(args.source), args.source.read_bytes())]
        for name, body in files:
            try:
                record = topology_for_map(body)
            except (ValueError, struct.error, IndexError) as exc:
                record = {'error': str(exc)}
            results.append({'file': name, **record})
        output = json.dumps({'maps': results}, indent=2, sort_keys=True)
        print(output)
        if args.json:
            args.json.write_text(output + '\n', encoding='utf-8')
        return 2 if any('error' in r or r.get('topology_review_count', 0) for r in results) else 0
    except (OSError, zipfile.BadZipFile) as exc:
        ap.exit(2, 'Unable to audit: ' + str(exc) + '\n')


if __name__ == '__main__':
    raise SystemExit(main())
