#!/usr/bin/env python3
"""Pure Python branch model of original DOS RAW9 command/animation state.

NOT Unity C# execution. Ground truth branch table: GOG ROTH.EXE LE object1
+0x22BB1, +0x22FD1..+0x23041.
"""
import unittest


class DosState:
    def __init__(self, modifier):
        self.command = (modifier | 0x20) & 255
        self.flags = 0x80 if (self.command & 2) == 0 else 0
        self.timer = 0
        self.active = True

    @property
    def reverse(self): return bool(self.flags & 0x80)

    @property
    def waiting(self): return bool(self.flags & 0x40)

    def retrigger(self, repeat, timeout):
        if not self.active or not (self.command & 0x20) or not repeat or timeout:
            return False
        self.flags ^= 0x80
        self.command ^= 0x02
        return True

    def consume_wait(self, ticks):
        if not self.waiting: return False
        if ticks < 0: return True
        elapsed_word = ticks & 65535
        signed = lambda v: v if v < 32768 else v - 65536
        # x86 SUB sets OF; signed JG tests original operands, not
        # merely the sign of the wrapped 16-bit subtraction result.
        keep_waiting = signed(self.timer) > signed(elapsed_word)
        self.timer = (self.timer - elapsed_word) & 65535
        if not keep_waiting:
            self.flags &= 0xBF
        return True

    def complete(self, repeat, timeout):
        if not self.active: return False
        if self.flags & 0x10:
            self.flags ^= 0x90
            return True
        if timeout == 0:
            self.command &= 0xDE
            if repeat: self.command ^= 2
            else: self.command |= 8
            self.active = False
            return False
        if repeat and self.flags & 0x20:
            self.command &= 0xDE
            self.active = False
            return False
        self.flags ^= 0xE0
        self.timer = timeout
        return True


class DosStateModelTests(unittest.TestCase):
    def test_initial_reverse_without_bit02(self):
        self.assertTrue(DosState(0).reverse)

    def test_initial_forward_with_bit02(self):
        self.assertFalse(DosState(2).reverse)

    def test_initial_preserve_unrelated_modifier(self):
        self.assertEqual(DosState(0x42).command, 0x62)

    def test_retrigger_requires_repeat(self):
        self.assertFalse(DosState(0).retrigger(False, 0))

    def test_retrigger_requires_zero_timeout(self):
        self.assertFalse(DosState(0).retrigger(True, 1))

    def test_retrigger_xors_two_bits(self):
        s = DosState(0)
        self.assertTrue(s.retrigger(True, 0))
        self.assertEqual((s.flags, s.command), (0, 0x22))
        self.assertTrue(s.retrigger(True, 0))
        self.assertEqual((s.flags, s.command), (0x80, 0x20))

    def test_completion_zero_timeout_disables(self):
        s = DosState(2)
        self.assertFalse(s.complete(False, 0))
        self.assertEqual(s.command, 0x0A)

    def test_completion_zero_timeout_repeat_xors_direction(self):
        s = DosState(2)
        self.assertFalse(s.complete(True, 0))
        self.assertEqual(s.command, 0)

    def test_countdown_arms_reversal(self):
        s = DosState(2)
        self.assertTrue(s.complete(False, 10))
        self.assertEqual((s.flags, s.timer), (0xE0, 10))

    def test_countdown_before_timeout(self):
        s = DosState(2); s.complete(False, 10)
        self.assertTrue(s.consume_wait(4))
        self.assertTrue(s.waiting)
        self.assertEqual(s.timer, 6)

    def test_countdown_finishes_exactly(self):
        s = DosState(2); s.complete(False, 10)
        self.assertTrue(s.consume_wait(10))
        self.assertFalse(s.waiting)
        self.assertEqual(s.timer, 0)

    def test_countdown_wrap_signed_comparison(self):
        s = DosState(2); s.complete(False, 2)
        self.assertTrue(s.consume_wait(4))
        self.assertEqual(s.timer, 65534)
        self.assertFalse(s.waiting)

    def test_wait_not_consumed_when_not_waiting(self):
        s = DosState(0)
        self.assertFalse(s.consume_wait(10))

    def test_repeat_timeout_first_complete_arms_return(self):
        s = DosState(2)
        self.assertTrue(s.complete(True, 12))
        self.assertEqual((s.flags, s.timer), (0xE0, 12))

    def test_repeat_timeout_second_complete_terminates(self):
        s = DosState(2); s.complete(True, 12); s.consume_wait(12)
        self.assertFalse(s.complete(True, 12))
        self.assertEqual(s.command, 0x02)

    def test_no_repeat_timeout_repeated_reverse(self):
        s = DosState(2); s.complete(False, 2); s.consume_wait(2)
        self.assertTrue(s.complete(False, 2))
        self.assertEqual(s.flags, 0x40)

    def test_special_phase_10_preempts_completion(self):
        s = DosState(2); s.flags = 0x10
        self.assertTrue(s.complete(True, 0))
        self.assertEqual(s.flags, 0x80)
        self.assertTrue(s.active)

    def test_completed_state_retrigger_not_allowed(self):
        s = DosState(0); s.complete(False, 0)
        self.assertFalse(s.retrigger(True, 0))

    def test_new_state_from_finished_modifier_preserves_disabled(self):
        s = DosState(0); s.complete(False, 0)
        self.assertEqual(s.command & 8, 8)

    def test_zero_tick_does_not_release_wait(self):
        s = DosState(2); s.complete(True, 4)
        self.assertTrue(s.consume_wait(0))
        self.assertTrue(s.waiting)

    def test_signed_overflow_negative_wait_no_false_keep(self):
        s = DosState(2); s.complete(False, 0x8000)
        self.assertTrue(s.consume_wait(1))
        self.assertEqual(s.timer, 0x7fff)
        self.assertFalse(s.waiting)  # -32768 > +1 is false even though wrapped result is +32767

    def test_signed_overflow_positive_wait_kept(self):
        s = DosState(2); s.complete(False, 0x7fff)
        self.assertTrue(s.consume_wait(0xffff))
        self.assertEqual(s.timer, 0x8000)
        self.assertTrue(s.waiting)  # 32767 > -1 is true even though wrapped result is -32768

    def test_signed_jg_reference_matrix(self):
        def signed(v): return v if v < 32768 else v - 65536
        for remaining in (0, 1, 45, 32767, 32768, 32769, 65534, 65535):
            for elapsed in (0, 1, 45, 32767, 32768, 65535):
                with self.subTest(remaining=remaining, elapsed=elapsed):
                    s = DosState(2); s.complete(False, remaining if remaining else 1)
                    s.timer = remaining
                    s.flags |= 0x40
                    s.consume_wait(elapsed)
                    self.assertEqual(s.waiting, signed(remaining) > signed(elapsed))
                    self.assertEqual(s.timer, (remaining - elapsed) & 0xffff)

    def test_signed_timer_countdown_restores_wait_only_after_arm(self):
        s=DosState(2)
        self.assertFalse(s.consume_wait(10))
        s.complete(False, 5)
        self.assertTrue(s.consume_wait(2))
        self.assertTrue(s.waiting)
        self.assertTrue(s.consume_wait(3))
        self.assertFalse(s.waiting)
        self.assertEqual(s.timer, 0)

    def test_unrelated_command_modifiers_separate(self):
        commands = {3: DosState(0), 9: DosState(2)}
        commands[3].retrigger(True, 0)
        self.assertEqual(commands[9].command, 0x22)


if __name__ == '__main__':
    unittest.main(verbosity=2)
