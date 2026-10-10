#!/usr/bin/env python3
"""Synthetic tests for state transitions and signature guards (no retail EXE)."""
import unittest
from unittest import mock

import audit_roth_opcode9_states as mod


class CompletionTests(unittest.TestCase):
    def test_no_timeout_no_repeat_sets_disabled(self):
        s=mod.complete(0,0,0)
        self.assertEqual(s['branch'],'no_repeat_no_timeout_disable_command')
        self.assertEqual((s['command_state_and_mask'],s['command_state_or_mask'],s['callback_return']),
                         (0xde,0x08,-1))

    def test_repeat_without_timeout_flips_command_direction_bit(self):
        s=mod.complete(0,0x20,0)
        self.assertEqual(s['branch'],'repeat_no_timeout_toggle_command')
        self.assertEqual((s['command_state_and_mask'],s['command_state_xor_mask'],s['callback_return']),
                         (0xde,0x02,-1))

    def test_timeout_non_repeat_initial(self):
        s=mod.complete(0x80,0,9)
        self.assertEqual(s['branch'],'arm_timeout_reverse')
        self.assertEqual(s['animation_flags'],0x60)
        self.assertEqual(s['animation_timeout_word'],9)
        self.assertEqual(s['callback_return'],0)

    def test_timeout_repeat_first_phase(self):
        s=mod.complete(0,0x20,9)
        self.assertEqual(s['branch'],'arm_timeout_reverse')
        self.assertEqual(s['animation_flags'],0xe0)

    def test_timeout_repeat_second_phase_finishes(self):
        s=mod.complete(0x20,0x20,9)
        self.assertEqual((s['branch'],s['callback_return'],s['command_state_and_mask']),
                         ('timeout_repeat_phase_finish',-1,0xde))

    def test_timeout_without_repeat_phase_still_arms(self):
        s=mod.complete(0x20,0,9)
        self.assertEqual((s['branch'],s['animation_flags']),('arm_timeout_reverse',0xc0))

    def test_special_flag_has_precedence_over_all_other_conditions(self):
        for repeat in (0,0x20):
            for timeout in (0,10):
                s=mod.complete(0x10,repeat,timeout)
                self.assertEqual(s['branch'],'special_direction_flip')
                self.assertEqual(s['animation_flags'],0x80)
                self.assertEqual(s['callback_return'],0)

    def test_state_byte_masks_are_8_bit(self):
        for flags in (0,0x10,0x20,0x30,0xe0,0xff):
            for cmd in (0,0x20):
                for timeout in (0,1,65535):
                    s=mod.complete(flags,cmd,timeout)
                    self.assertTrue(0<=s['animation_flags']<=255)

    def test_invalid_arguments_rejected(self):
        for values in ((-1,0,0),(256,0,0),(0,-1,0),(0,256,0),(0,0,-1),(0,0,65536)):
            with self.assertRaises(ValueError):mod.complete(*values)


class DelayTests(unittest.TestCase):
    def test_mid_delay_keeps_wait_flag(self):
        s=mod.elapsed_wait(0x40,12,4)
        self.assertEqual((s['remaining_word'],s['animation_flags'],s['waiting']), (8,0x40,True))

    def test_exact_timeout_clears_wait_flag(self):
        s=mod.elapsed_wait(0xC0,10,10)
        self.assertEqual((s['remaining_word'],s['animation_flags'],s['waiting']), (0,0x80,False))

    def test_elapsed_exceeds_timeout(self):
        s=mod.elapsed_wait(0x60,2,5)
        self.assertEqual((s['remaining_word'],s['animation_flags'],s['waiting']), (65533,0x20,False))

    def test_signed_compare_not_unsigned(self):
        s=mod.elapsed_wait(0x40,0xffff,1)
        self.assertFalse(s['waiting'])

    def test_negative_inputs_rejected(self):
        with self.assertRaises(ValueError):mod.elapsed_wait(0x40,1,-1)


class BinaryAuditTests(unittest.TestCase):
    def mock_image(self):
        # Fresh synthetic object-1 bytes with only known instructions at
        # their original offsets; does NOT include any DOS proprietary code.
        image=bytearray(0x23100)
        for offset,signature in mod.SITES.values():
            code=bytes.fromhex(signature)
            image[offset:offset+len(code)]=code
        return image

    def run_mock_audit(self,image,reloc_offset=0x1570c):
        fixup={'target_type':0,'target_index':3,'target_object_offset':reloc_offset}
        with mock.patch.object(mod,'verify_dispatchers',return_value={'opcode_9_update_object_offset':0x22bd9}), \
             mock.patch.object(mod,'verify',return_value={'sha256':'synthetic'}), \
             mock.patch.object(mod,'initialized_object_bytes',return_value=image), \
             mock.patch.object(mod,'parse_fixups',return_value=[]), \
             mock.patch.object(mod,'index_relocations',return_value={(1,0x22bec):fixup}):
            return mod.audit(b'')

    def test_all_signatures_are_rechecked(self):
        summary=self.run_mock_audit(self.mock_image())
        self.assertGreaterEqual(summary['verified_signature_count'],25)
        self.assertEqual(len(summary['completion_state_truth_table']),8)

    def test_bad_completion_code_rejected(self):
        image=self.mock_image()
        image[0x2300b]=0xcc
        with self.assertRaises(mod.SignatureMismatch):self.run_mock_audit(image)

    def test_bad_direction_test_rejected(self):
        image=self.mock_image()
        image[0x22bfa]=0xcc
        with self.assertRaises(mod.SignatureMismatch):self.run_mock_audit(image)

    def test_wrong_frame_delta_relocation_rejected(self):
        with self.assertRaises(mod.SignatureMismatch):
            self.run_mock_audit(self.mock_image(),reloc_offset=0x15708)


if __name__=='__main__':unittest.main(verbosity=2)
