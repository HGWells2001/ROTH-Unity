#!/usr/bin/env python3
"""Unit tests of PIT/RAW9 evidence and signed-16-bit arithmetic."""
import unittest
from unittest import mock
import audit_roth_pit_raw9 as m

class TickMathTests(unittest.TestCase):
    def test_nominal_irq_rate(self):
        self.assertAlmostEqual(m.PIT_INPUT_HZ/0x4216,70.5273673,places=6)
    def test_initial_divisor_rate(self):
        self.assertAlmostEqual(m.PIT_INPUT_HZ/0x4242,70.3444169,places=6)
    def test_mode_three_alias_seven(self):
        word=0x3e
        self.assertEqual((word>>6)&3,0)  # channel zero
        self.assertEqual((word>>4)&3,3)  # lo+hi
        self.assertEqual((word>>1)&7,7)  # mode7 maps to mode3
        self.assertEqual(word&1,0)       # binary
    def test_small_positive_forward(self):
        self.assertEqual(m.axis_step(current=100,raw_bound=-100,elapsed=2,speed_byte=10),
                         dict(delta=20,reached=False,clamped=False,target=200))
    def test_forward_clamps_at_bound(self):
        self.assertEqual(m.axis_step(190,-100,4,10),
                         dict(delta=10,reached=True,clamped=True,target=200))
    def test_reverse_clamps_at_bound(self):
        self.assertEqual(m.axis_step(108,-50,3,10,reverse=True),
                         dict(delta=-8,reached=True,clamped=True,target=100))
    def test_already_at_target_no_motion(self):
        self.assertEqual(m.axis_step(200,-100,5,255)['delta'],0)
    def test_raw_target_wraps_signed_16(self):
        self.assertEqual(m.target_from_command(-20000),-25536)
    def test_minus_one_command_boundary(self):
        self.assertEqual(m.target_from_command(-1),2)
    def test_large_speed_multiplier_wraps_low16(self):
        self.assertEqual(m.axis_step(0,-15000,256,255)['delta'],-256)
    def test_frame_delta_can_be_negative(self):
        self.assertEqual(m.axis_step(100,-100,-1,10)['delta'],-10)
    def test_input_validation(self):
        cases=[(32768,0,1,1), (1,-32769,1,1), (1,0,0,256), (1,0,1<<31,1)]
        for vals in cases:
            with self.subTest(vals=vals),self.assertRaises(ValueError):m.axis_step(*vals)
    def test_low16_signed_conversion(self):
        self.assertEqual(m.int16(65535),-1)
        self.assertEqual(m.int16(32768),-32768)

class BinaryEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sites=max(off+len(bytes.fromhex(signature)) for off,signature in m.SITES.values())
    def fixture(self):
        code=bytearray(self.sites+10)
        for offset,h in m.SITES.values():
            raw=bytes.fromhex(h);code[offset:offset+len(raw)]=raw
        reloc={}
        for name,(pos,obj,offs) in m.RELOCATIONS.items():
            reloc[(1,pos)]={'target_type':0,'target_index':obj,'target_object_offset':offs}
        return code,reloc
    def audit_synthetic(self,code,relocs):
        with mock.patch.object(m,'audit_states',return_value={'opcode_9_update_object1_offset':'0x22bd9'}),\
             mock.patch.object(m,'verify',return_value={'sha256':'synthetic'}),\
             mock.patch.object(m,'initialized_object_bytes',return_value=code),\
             mock.patch.object(m,'parse_fixups',return_value=[]),\
             mock.patch.object(m,'index_relocations',return_value=relocs):
            return m.audit(b'mock')
    def test_all_expected_sites_and_relocs(self):
        code,reloc=self.fixture();r=self.audit_synthetic(code,reloc)
        self.assertEqual(r['evidence_sites'],len(m.SITES))
        self.assertEqual(r['verified_absolute_relocations'],len(m.RELOCATIONS))
        self.assertEqual(r['irq_reprogram_divisor'],16918)
        self.assertEqual(r['irq_default_restore_divisor'],65535)
    def test_site_mutation_rejected(self):
        code,reloc=self.fixture();code[0x2438]=0xff
        with self.assertRaisesRegex(m.EvidenceMismatch,'instruction mismatch'):
            self.audit_synthetic(code,reloc)
    def test_missing_relocation_rejected(self):
        code,reloc=self.fixture();del reloc[(1,0x23e1)]
        with self.assertRaisesRegex(m.EvidenceMismatch,'relocation mismatch'):
            self.audit_synthetic(code,reloc)
    def test_wrong_clock_object_rejected(self):
        code,reloc=self.fixture();reloc[(1,0x102d5)]['target_index']=1
        with self.assertRaises(m.EvidenceMismatch):self.audit_synthetic(code,reloc)
    def test_wrong_opcode9_callback_rejected(self):
        code,reloc=self.fixture()
        with mock.patch.object(m,'audit_states',return_value={'opcode_9_update_object1_offset':'0x22d51'}),\
             mock.patch.object(m,'verify',return_value={'sha256':'synthetic'}),\
             mock.patch.object(m,'initialized_object_bytes',return_value=code),\
             mock.patch.object(m,'parse_fixups',return_value=[]),\
             mock.patch.object(m,'index_relocations',return_value=reloc):
            with self.assertRaises(m.EvidenceMismatch):m.audit(b'mock')

if __name__=='__main__':unittest.main(verbosity=2)
