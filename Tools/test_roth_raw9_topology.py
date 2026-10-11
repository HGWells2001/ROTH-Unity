#!/usr/bin/env python3
"""Synthetic portal/shared-index fixture regressions; no retail assets."""
import struct
import unittest
from audit_roth_raw9_topology import topology_for_map


def two_sector_fixture(*, shared=False, duplicate_position=False, sister=False,
                       broken_sister=False, more_faces=False):
    data = bytearray(0x300)
    faces_base, sectors_base, vertex_base = 0x90, 0x30, 0x130
    vertices = [(0, 0), (100, 0), (100, 100), (0, 100),
                (100, 0) if duplicate_position else (150, 0),
                (100, 100) if duplicate_position else (150, 100), (200, 100), (200, 0)]
    vertex_size = 8 + len(vertices) * 12
    cmd_start = vertex_base + vertex_size
    struct.pack_into('<15H', data, 0, vertex_base, 0x70, sectors_base,
                     faces_base, 0xb0, 0xc0, vertex_base, 0x5257, 0, 0,
                     vertex_size, 0, 0, 90, 2)
    struct.pack_into('<H', data, faces_base-2, 8)
    for sector_id, idx, count in [(10, 0, 5 if more_faces else 4), (20, 1, 4)]:
        pos = sectors_base + 26*idx
        data[pos+13] = count
        struct.pack_into('<H', data, pos+14, faces_base+idx*4*12)
        struct.pack_into('<H', data, pos+20, sector_id)
    first = [0,1,2,3,4,5,6,7]
    if shared: first[4] = 1
    for i in range(8):
        pos = faces_base + i*12
        owner = sectors_base + (0 if i<4 else 26)
        nxt = (i//4)*4 + ((i+1)%4)
        sister_link = 0
        if sister:
            if i==1: sister_link = faces_base+4*12
            if i==4: sister_link = faces_base+1*12
        if broken_sister and i==1: sister_link = 0x432
        struct.pack_into('<6H',data,pos,8+first[i]*12,8+first[nxt]*12,0,owner,sister_link,0)
    struct.pack_into('<4H',data,vertex_base,vertex_size,8,0,len(vertices))
    for i,(x,y) in enumerate(vertices):
        struct.pack_into('<hh',data,vertex_base+8+i*12+8,x,y)
    struct.pack_into('<4H',data,cmd_start,30003,0,72,1)
    struct.pack_into('<HBBH6H',data,cmd_start+72,18,0,9,0,0x40,10,0,0,20,0)
    return data


class TopologyTests(unittest.TestCase):
    def report(self, **kwargs):
        return topology_for_map(two_sector_fixture(**kwargs))['commands'][0]['topology']

    def test_clean_case_still_requires_runtime_review(self):
        r = self.report()
        self.assertEqual(r['status'], 'no_static_hazard_detected')
        self.assertTrue(r['manual_review_required'])
    def test_shared_vertex_across_sectors(self):
        r = self.report(shared=True)
        self.assertEqual(r['external_vertex_reference_faces'], [4, 7])
        self.assertIn('selected_vertex_referenced_by_other_sector',r['hazards'])
    def test_coincident_independent_vertex(self):
        r = self.report(duplicate_position=True)
        self.assertIn(4,r['external_coincident_faces'])
        self.assertIn('external_distinct_vertex_at_selected_coordinates',r['hazards'])
    def test_portal_sister_with_selected_vertex(self):
        r=self.report(sister=True)
        self.assertEqual(r['cross_sector_sister_edges'][0]['face_index'],1)
        self.assertGreater(r['cross_sector_sister_edges'][0]['moving_endpoint_count'],0)
        self.assertIn('cross_sector_sister_face_touches_selected_vertex',r['hazards'])
    def test_invalid_sister_detected(self):
        r=self.report(broken_sister=True)
        self.assertEqual(r['invalid_sister_face_indices'],[1])
    def test_nonquad_flagged(self):
        r=self.report(more_faces=True)
        self.assertIn('sector_has_more_than_four_faces',r['hazards'])
    def test_combined_hazards_are_independent(self):
        r=self.report(shared=True, duplicate_position=True, sister=True)
        self.assertEqual(len(r['hazards']),3)
    def test_does_not_modify_original_map(self):
        src=two_sector_fixture(sister=True)
        original=src[:]
        topology_for_map(src)
        self.assertEqual(src,original)
    def test_csharp_helper_is_not_connected_to_mover(self):
        from pathlib import Path
        root=Path(__file__).resolve().parents[1]
        mover=(root/'Assets/ROTHUnity/Runtime/RothHorizontalSectorMover.cs').read_text()
        self.assertNotIn('RothRaw9TopologyInspector',mover)
    def test_truncated_face_section_rejected(self):
        data=two_sector_fixture()
        struct.pack_into('<H',data,0x90-2,4096)
        with self.assertRaises(ValueError):topology_for_map(data)

if __name__=='__main__':unittest.main(verbosity=2)
