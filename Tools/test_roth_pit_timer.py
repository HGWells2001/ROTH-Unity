#!/usr/bin/env python3
"""Synthetic-only regression checks for ROTH IRQ0/PIT frequency extraction.

No copyrighted binary or LE object is required to run the tests.
"""
import unittest
from audit_roth_pit_timer import (
    SITES, TARGET_FIXUPS, TimerAuditMismatch, hz_from_divisor,
    original_frame_delta, pit_control_word, signed16, verify_sites,
)


def fixture():
    code = bytearray(0x23000)
    for _name, (at, source) in SITES.items():
        code[at:at + len(bytes.fromhex(source))] = bytes.fromhex(source)
    refs = {}
    for _name, (at, target_object, target_offset) in TARGET_FIXUPS.items():
        refs[1, at] = {'source_type': 7, 'target_type': 0,
                       'target_index': target_object,
                       'target_object_offset': target_offset}
    return code, refs


class PitTests(unittest.TestCase):
    def test_mode3_alias_and_channel0(self):
        self.assertEqual(pit_control_word(0x3e), {
            'counter': 0, 'access_mode': 3, 'mode_bits': 7,
            'canonical_mode': 3, 'bcd': False})

    def test_control_byte_range_checked(self):
        for x in (-1, 256, 3.5):
            with self.subTest(x=x), self.assertRaises(ValueError):pit_control_word(x)

    def test_pit_divisors(self):
        self.assertAlmostEqual(hz_from_divisor(0x4216),70.52736730109942)
        self.assertAlmostEqual(hz_from_divisor(0x4242),70.34441693196557)
        self.assertGreater(hz_from_divisor(0x4216),hz_from_divisor(0x4242))

    def test_invalid_divisors(self):
        for n in (0,-1,65537,3.5):
            with self.subTest(n=n), self.assertRaises(ValueError):hz_from_divisor(n)

    def test_fixture_evidence_matches(self):
        code, refs = fixture()
        self.assertEqual(verify_sites(code, refs),(15,9))

    def test_all_sites_detect_corruption(self):
        for _name, (at, sig) in SITES.items():
            code, refs = fixture()
            code[at] ^= 1
            with self.subTest(name=_name), self.assertRaises(TimerAuditMismatch):
                verify_sites(code, refs)

    def test_all_relocations_detect_wrong_target_object(self):
        for _name, (at, _o, _off) in TARGET_FIXUPS.items():
            code, refs = fixture()
            refs[1,at]['target_index'] = 99
            with self.subTest(name=_name), self.assertRaises(TimerAuditMismatch):
                verify_sites(code, refs)

    def test_all_relocations_detect_changed_offset(self):
        for _name, (at, _o, _off) in TARGET_FIXUPS.items():
            code, refs = fixture()
            refs[1,at]['target_object_offset'] += 2
            with self.subTest(name=_name), self.assertRaises(TimerAuditMismatch):
                verify_sites(code, refs)

    def test_missing_source_relocation_fails(self):
        code, refs=fixture();del refs[1,0x23e1]
        with self.assertRaises(TimerAuditMismatch):verify_sites(code,refs)

    def test_counter_to_signed16(self):
        self.assertEqual(signed16(0x7fff),32767)
        self.assertEqual(signed16(0x8000),-32768)
        self.assertEqual(signed16(0xffff),-1)

    def test_signed16_range_checked(self):
        for n in (-1,65536,1.5):
            with self.subTest(n=n), self.assertRaises(ValueError):signed16(n)

    def test_usual_delta(self):
        self.assertEqual(original_frame_delta(200,195),5)
        self.assertEqual(original_frame_delta(195,200),-5)

    def test_sign_boundary_is_not_modular(self):
        # The two MOVSX loads and a 32-bit SUB yield a big negative delta.
        self.assertEqual(original_frame_delta(0x8000,0x7fff),-65535)
        self.assertEqual(original_frame_delta(0x0000,0xffff),1)

    def test_full_wrap_is_not_modular(self):
        self.assertEqual(original_frame_delta(0x0000,0xffff),1)
        self.assertEqual(original_frame_delta(0xffff,0x0000),-1)

    def test_delay_ticks_use_pit_not_assumed_decimal_tenths(self):
        self.assertAlmostEqual(100/hz_from_divisor(0x4216),1.4178893077502008)
        self.assertNotAlmostEqual(100/hz_from_divisor(0x4216),10.0)


if __name__=='__main__': unittest.main(verbosity=2)
