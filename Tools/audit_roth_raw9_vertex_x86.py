#!/usr/bin/env python3
"""Verify four RAW9 vertex dereferences and signed sort in an owned ROTH.EXE.

No binary, full disassembly or extracted code is exported. Use exact LE
relocation addresses so a matching byte pattern alone is not enough.
"""
import argparse
import json
from pathlib import Path
from inspect_roth_le import verify, initialized_object_bytes
from inspect_roth_fixups import parse_fixups
from audit_roth_opcode9 import index_relocations

SITES = {
    'x_y_axis_bit': (0x22ae3, 'f6 46 06 40'),
    'vertex_record_x_plus_8': (0x22ade, 'ba 08 00 00 00'),
    'vertex_record_y_plus_10': (0x22ae9, '83 c2 02'),
    'first_face_pointer_field_plus_14': (0x22af8, '66 8b 4c 0b 0e'),
    'face0_first_vertex': (0x22afd, '66 8b 04 0b'),
    'face1_first_vertex': (0x22b10, '66 8b 44 0b 0c'),
    'face2_first_vertex': (0x22b25, '66 8b 44 0b 18'),
    'face3_first_vertex': (0x22b3a, '66 8b 44 0b 24'),
    'sort_signed_short': (0x22b59, '66 3b 43 04 7e 05'),
    'sorted_ref_0': (0x22b6f, '66 8b 44 24 02 66 89 47 0e'),
    'sorted_ref_1': (0x22b78, '66 8b 44 24 06 66 89 47 10'),
    'sorted_ref_2': (0x22b81, '66 8b 44 24 0a 66 89 47 12'),
    'sorted_ref_3': (0x22b8a, '66 8b 44 24 0e 66 89 47 14'),
    'update_four_vertices_0': (0x22c7e, '66 8b 56 0e 66 01 04 1a'),
    'update_four_vertices_1': (0x22c86, '66 8b 56 10 66 01 04 1a'),
    'update_four_vertices_3': (0x22c8e, '66 8b 56 14 66 01 04 1a'),
    'update_four_vertices_2': (0x22c96, '66 8b 56 12 66 01 04 1a'),
}
# Source object=1, source operand offset, target object=3, target offset
POINTER_FIXUPS = {
    'map_base': (0x22aee, 3, 0x20e88),
    'vertex_base': (0x22af4, 3, 0x20e8c),
    'update_vertex_base_forward': (0x22c16, 3, 0x20e8c),
    'update_vertex_base_reverse': (0x22c55, 3, 0x20e8c),
}


def audit(data):
    r=verify(data)
    code=initialized_object_bytes(data,r,1)
    fixups=index_relocations(parse_fixups(data,r))
    for name,(where,signature) in SITES.items():
        signature=bytes.fromhex(signature)
        if code[where:where+len(signature)]!=signature:
            raise ValueError(f'x86 instruction mismatch at {name} object1+{where:#x}')
    for name,(where,obj,target) in POINTER_FIXUPS.items():
        record=fixups.get((1,where))
        if not record or (record['target_type'],record['target_index'],record['target_object_offset'])!=(0,obj,target):
            raise ValueError(f'LE target relocation mismatch at {name} object1+{where:#x}')
    return {
        'exe_sha256':r['sha256'],
        'verified_instruction_sites':len(SITES),
        'verified_global_pointer_relocations':len(POINTER_FIXUPS),
        'opcode9_initializer_object1_offset':'0x22a99',
        'opcode9_updater_object1_offset':'0x22bd9',
        'sector_first_face_pointer_byte_offset':14,
        'raw_face_stride_bytes':12,
        'face_first_vertex_pointer_byte_offset':0,
        'selected_first_face_offsets':[0,12,24,36],
        'vertex_record_component_offsets':{'X':8,'Y':10},
        'selection_order':'four coordinate-reference pairs, ascending signed16 stable bubble sort',
        'inflight_update':'same signed16 coordinate delta added to all four selected vertex component words',
        'limits':['Only first four face records accessed by RAW9 for this GOG executable.',
                  'This verifies raw data selection, not safe Unity portal/mesh mutation or DOS tick synchrony.'],
    }


def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('exe',type=Path)
    ap.add_argument('--json',type=Path)
    a=ap.parse_args()
    result=audit(a.exe.read_bytes())
    encoded=json.dumps(result,indent=2,sort_keys=True)
    print(encoded)
    if a.json:a.json.write_text(encoded+'\n',encoding='utf-8')
    return 0

if __name__=='__main__':raise SystemExit(main())
