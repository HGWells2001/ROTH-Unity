#!/usr/bin/env python3
"""Synthetic RAW9 four-vertex motion model tests, NOT a Unity runtime test.

The fixture mirrors selected coordinate writes. These tests protect the
geometry assumptions and preflight; real C# Play Mode still must be tested.
"""
import struct
import unittest

from audit_roth_raw9_geometry import audit_raw
from audit_roth_raw9_topology import topology_for_map
from test_roth_raw9_topology import two_sector_fixture


def selected_motion(raw, delta, along_x=True):
    src = bytes(raw)
    report = topology_for_map(src)
    if not report['commands']:
        raise ValueError('No opcode9 command')
    command = report['commands'][0]
    if 'error' in command:
        raise ValueError(command['error'])
    if command['sector_face_count'] != 4:
        raise ValueError('Only exactly four faces are allowed')
    if command['topology']['hazards']:
        raise ValueError('Static topology hazards: ' + str(command['topology']['hazards']))
    if (command['axis'] == 'X') != along_x:
        raise ValueError('Axis mismatch')
    first = command['vertices_sorted']
    header = struct.unpack_from('<15H', src)
    face0 = header[3]
    # Exactly four consecutive, closed/ordered original edges are required.
    for i in range(4):
        face = face0 + i*12
        next_face = face0 + ((i+1)%4)*12
        if struct.unpack_from('<H',src,face+2)[0] != struct.unpack_from('<H',src,next_face)[0]:
            raise ValueError('Open or unordered face ring')
    axis_offset = 8 if along_x else 10
    addresses = [header[0] + 8 + index*12 + axis_offset for index in first]
    candidates = [struct.unpack_from('<h',src,at)[0] + delta for at in addresses]
    if any(v < -32768 or v > 32767 for v in candidates):
        raise ValueError('16-bit coordinate out of range')
    clone = bytearray(src)
    for at, value in zip(addresses,candidates):
        struct.pack_into('<h',clone,at,value)
    return bytes(clone)


def y_fixture():
    d = two_sector_fixture()
    vertex_base = struct.unpack_from('<H',d,0)[0]
    vertex_section_size = struct.unpack_from('<H',d,20)[0]
    command = vertex_base + vertex_section_size + 72
    struct.pack_into('<H',d,command+6,0)
    return d


class SimulatedSelectionTests(unittest.TestCase):
    def setUp(self):
        self.src = bytes(two_sector_fixture())
    def test_base_map_has_one_valid_command(self):
        self.assertEqual(audit_raw(self.src)['valid_count'],1)
    def test_four_indices_are_unique(self):
        x = audit_raw(self.src)['commands'][0]['vertices_sorted']
        self.assertEqual(len(x),4)
        self.assertEqual(len(set(x)),4)
    def test_non_selected_coordinates_remain_unchanged(self):
        moved = selected_motion(self.src,12)
        vstart = struct.unpack_from('<H',self.src,0)[0]
        for i in range(4,8):
            self.assertEqual(self.src[vstart+8+i*12:vstart+8+(i+1)*12],
                             moved[vstart+8+i*12:vstart+8+(i+1)*12])
    def test_x_shift_changes_only_selected_x_components(self):
        moved=selected_motion(self.src,18)
        vstart=struct.unpack_from('<H',self.src,0)[0]
        for i in range(4):
            at=vstart+8+i*12
            a=struct.unpack_from('<hh',self.src,at+8)
            b=struct.unpack_from('<hh',moved,at+8)
            self.assertEqual(b,(a[0]+18,a[1]))
    def test_y_shift_changes_only_selected_y_components(self):
        source=bytes(y_fixture())
        moved=selected_motion(source,-10,along_x=False)
        vstart=struct.unpack_from('<H',source,0)[0]
        for i in range(4):
            at=vstart+8+i*12
            a=struct.unpack_from('<hh',source,at+8)
            b=struct.unpack_from('<hh',moved,at+8)
            self.assertEqual(b,(a[0],a[1]-10))
    def test_idempotent_zero_shift(self):
        self.assertEqual(selected_motion(self.src,0),self.src)
    def test_two_steps_equivalent_to_one(self):
        self.assertEqual(selected_motion(selected_motion(self.src,3),7),
                         selected_motion(self.src,10))
    def test_opposite_shifts_restore_original(self):
        self.assertEqual(selected_motion(selected_motion(self.src,30),-30),self.src)
    def test_shared_vertex_hazard_rejected(self):
        with self.assertRaisesRegex(ValueError,'topology'):
            selected_motion(two_sector_fixture(shared=True),10)
    def test_linked_portal_hazard_rejected(self):
        with self.assertRaisesRegex(ValueError,'topology'):
            selected_motion(two_sector_fixture(sister=True),10)
    def test_coincident_vertex_hazard_rejected(self):
        with self.assertRaisesRegex(ValueError,'topology'):
            selected_motion(two_sector_fixture(duplicate_position=True),10)
    def test_nonquad_rejected(self):
        with self.assertRaisesRegex(ValueError,'four faces'):
            selected_motion(two_sector_fixture(more_faces=True),10)
    def test_y_does_not_accept_x_command(self):
        with self.assertRaisesRegex(ValueError,'Axis mismatch'):
            selected_motion(self.src,3,along_x=False)
    def test_overflow_does_not_modify_input(self):
        d=bytearray(two_sector_fixture())
        vertexbase=struct.unpack_from('<H',d,0)[0]
        struct.pack_into('<h',d,vertexbase+8+0*12+8,32765)
        original=bytes(d)
        with self.assertRaisesRegex(ValueError,'out of range'):
            selected_motion(d,9)
        self.assertEqual(bytes(d),original)
    def test_underrange_rejected_before_write(self):
        d=bytearray(two_sector_fixture())
        vertexbase=struct.unpack_from('<H',d,0)[0]
        struct.pack_into('<h',d,vertexbase+8+0*12+8,-32760)
        with self.assertRaisesRegex(ValueError,'out of range'):
            selected_motion(d,-20)
    def test_invalid_edge_order_rejected(self):
        d=bytearray(two_sector_fixture())
        facebase=struct.unpack_from('<H',d,6)[0]
        struct.pack_into('<H',d,facebase+2,8+7*12)
        with self.assertRaisesRegex(ValueError,'Open or unordered'):
            selected_motion(d,5)


if __name__=='__main__':
    unittest.main(verbosity=2)
