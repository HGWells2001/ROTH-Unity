#!/usr/bin/env python3
"""Synthetic RAW map fixtures, without commercial assets."""
import struct
import unittest
from audit_roth_raw9_geometry import audit_raw, select_vertices


def raw_fixture(values=(100,20,60,40), faces=4, axis_x=True):
    data=bytearray(0x210)
    vstart,sectorstart,facestart=0xB0,0x30,0x4C
    vsize=8+4*12
    cstart=vstart+vsize
    cmdsize=90
    struct.pack_into('<15H',data,0,vstart,0x70,sectorstart,facestart,0x80,0xA0,
                     vstart,0x5257,0,0,vsize,0,0,cmdsize,1)
    sector=sectorstart
    data[sector+13]=faces
    struct.pack_into('<HH',data,sector+14,facestart,0)
    struct.pack_into('<H',data,sector+20,123)
    struct.pack_into('<H',data,facestart-2,4)
    for i in range(4):
        struct.pack_into('<6H',data,facestart+i*12,8+i*12,8+((i+1)%4)*12,
                         0,sector,0,0)
    struct.pack_into('<4H',data,vstart,vsize,8,0,4)
    for i,value in enumerate(values):
        struct.pack_into('<hh',data,vstart+8+i*12+8,value,50-i*10)
    struct.pack_into('<4H',data,cstart,30003,0,72,1)
    struct.pack_into('<H',data,cstart+68,72)
    command=cstart+72
    struct.pack_into('<HBBH6H',data,command,18,0,9,0,0x40 if axis_x else 0,123,0,0,10,0)
    return data

class GeometryTests(unittest.TestCase):
    def test_four_first_face_indices_sorted_x(self):
        r=audit_raw(raw_fixture())
        self.assertEqual(r['valid_count'],1)
        self.assertEqual(r['commands'][0]['vertices_sorted'],[1,3,2,0])
        self.assertEqual(r['commands'][0]['coordinate_range'],[20,100])
        self.assertEqual(r['commands'][0]['raw_vertex_offsets_sorted'],[20,44,32,8])
    def test_y_axis_uses_vertex_plus_ten(self):
        r=audit_raw(raw_fixture(axis_x=False))
        self.assertEqual(r['commands'][0]['vertices_sorted'],[3,2,1,0])
        self.assertEqual(r['commands'][0]['coordinate_range'],[20,50])
    def test_stable_order_for_ties(self):
        r=audit_raw(raw_fixture(values=(3,2,2,1)))
        self.assertEqual(r['commands'][0]['vertices_sorted'],[3,1,2,0])
    def test_non_quad_warning(self):
        r=audit_raw(raw_fixture(faces=5))
        self.assertTrue(r['commands'][0]['non_quad_sector'])
    def test_insufficient_faces_rejected(self):
        r=audit_raw(raw_fixture(faces=3))
        self.assertIn('four',r['commands'][0]['error'])
    def test_duplicate_vertex_rejected(self):
        d=raw_fixture()
        struct.pack_into('<H',d,0x4c+12,8)
        self.assertIn('duplicate',audit_raw(d)['commands'][0]['error'])
    def test_wrong_face_owner_rejected(self):
        d=raw_fixture()
        struct.pack_into('<H',d,0x4c+2*12+6,0x99)
        self.assertIn('sector pointer',audit_raw(d)['commands'][0]['error'])
    def test_unaligned_vertex_rejected(self):
        d=raw_fixture()
        struct.pack_into('<H',d,0x4c,9)
        self.assertIn('vertex record',audit_raw(d)['commands'][0]['error'])
    def test_unknown_sector_rejected(self):
        d=raw_fixture()
        struct.pack_into('<H',d,0xB0+56+72+8,222)
        self.assertEqual(audit_raw(d)['commands'][0]['error'],'unknown sector id')
    def test_truncated_command_rejected(self):
        d=raw_fixture()
        struct.pack_into('<H',d,0xB0+56+72,100)
        with self.assertRaisesRegex(ValueError,'command length'):
            audit_raw(d)
    def test_truncated_vertex_table_rejected(self):
        d=raw_fixture()
        struct.pack_into('<H',d,0xB0+6,15)
        with self.assertRaisesRegex(ValueError,'vertex table truncated'):
            audit_raw(d)
    def test_csharp_selection_not_connected_to_mover(self):
        from pathlib import Path
        root=Path(__file__).resolve().parents[1]
        helper=(root/'Assets/ROTHUnity/Core/RothRaw9VertexInspector.cs').read_text()
        self.assertIn('selected.Coordinates[j] > selected.Coordinates[j + 1]',helper)
        mover=root/'Assets/ROTHUnity/Runtime/RothHorizontalSectorMover.cs'
        if mover.exists():self.assertNotIn('RothRaw9VertexInspector.TrySelect',mover.read_text())


class DosEvidenceTests(unittest.TestCase):
    def fixture(self):
        from audit_roth_raw9_vertex_x86 import SITES, POINTER_FIXUPS
        n=max(o+len(bytes.fromhex(raw)) for o,raw in SITES.values())
        code=bytearray(n+16)
        for off,raw in SITES.values():
            raw=bytes.fromhex(raw)
            code[off:off+len(raw)]=raw
        reloc={}
        for off,obj,target in POINTER_FIXUPS.values():
            reloc[(1,off)]={'target_type':0,'target_index':obj,'target_object_offset':target}
        return code,reloc
    def run_mock(self,code,reloc):
        from unittest.mock import patch
        import audit_roth_raw9_vertex_x86 as audit
        with patch.object(audit,'verify',return_value={'sha256':'fixture'}), \
             patch.object(audit,'initialized_object_bytes',return_value=code), \
             patch.object(audit,'parse_fixups',return_value=[]), \
             patch.object(audit,'index_relocations',return_value=reloc):
            return audit.audit(b'fixture')
    def test_x86_site_and_fixup_counts(self):
        code,reloc=self.fixture()
        report=self.run_mock(code,reloc)
        self.assertEqual(report['verified_instruction_sites'],17)
        self.assertEqual(report['verified_global_pointer_relocations'],4)
    def test_x86_face_stride_corruption_rejected(self):
        code,reloc=self.fixture()
        code[0x22b10+3]=0xff
        with self.assertRaisesRegex(ValueError,'face1_first_vertex'):
            self.run_mock(code,reloc)
    def test_x86_wrong_global_target_rejected(self):
        code,reloc=self.fixture()
        reloc[(1,0x22af4)]['target_object_offset']=12345
        with self.assertRaisesRegex(ValueError,'vertex_base'):
            self.run_mock(code,reloc)
    def test_x86_wrong_instruction_branch_rejected(self):
        code,reloc=self.fixture()
        code[0x22b59+4]=0x7f
        with self.assertRaisesRegex(ValueError,'sort_signed_short'):
            self.run_mock(code,reloc)

if __name__=='__main__':unittest.main(verbosity=2)
