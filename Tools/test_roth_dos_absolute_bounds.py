#!/usr/bin/env python3
"""16 pure-model tests of DOS RAW9 single-shot coordinate bounds.

Independent from Unity. This models selected x86 branches at object1
+0x22c00..+0x22c79; it does not establish complete DOS lifecycle,
geometry collision correctness, or C# compilation.
"""
import unittest

def s16(value):
    value &= 0xffff
    return value if value < 32768 else value - 65536

def plan(low, high, start, end, reverse, offset=0):
    if low > high:
        return None
    target = s16(-2 * (end if reverse else start))
    current = low if reverse else high
    if (reverse and target > current) or (not reverse and target < current):
        return None
    result = offset + target - current
    return result if -2147483648 <= result <= 2147483647 else None

def step(current, bound, ticks, speed, reverse):
    target = s16(-2 * bound)
    if current == target:
        return 0, True
    raw = s16((-1 if reverse else 1) * ticks * speed)
    candidate = s16(current + raw)
    if (reverse and candidate < target) or (not reverse and candidate > target):
        raw = s16(raw - s16(candidate - target))
    return raw, s16(current + raw) == target

class DosRaw9AbsoluteBoundsTests(unittest.TestCase):
    def test_forward_uses_max(self):
        self.assertEqual(plan(-40, 10, -20, 20, False), 30)
    def test_reverse_uses_min(self):
        self.assertEqual(plan(-40, 10, -20, 20, True), 0)
    def test_reverse_reaches_negative_bound(self):
        self.assertEqual(plan(-10, 20, 2, 20, True), -30)
    def test_forward_rejects_unreachable(self):
        self.assertIsNone(plan(-10, 20, 0, 20, False))
    def test_reverse_rejects_unreachable(self):
        self.assertIsNone(plan(-10, 20, 0, -15, True))
    def test_axis_offset_retained(self):
        self.assertEqual(plan(-5, 10, -10, 20, False, 7), 17)
    def test_target_wrap_signed(self):
        self.assertEqual(plan(-20, 200, 20000, 20, True), -20)
    def test_no_direction_for_zero_distance(self):
        self.assertEqual(plan(-20, 10, -5, 10, False), 0)
    def test_overflow_guard(self):
        self.assertIsNone(plan(-20, 20, -20, 0, False, 2**31-1))
    def test_invalid_range(self):
        self.assertIsNone(plan(10, -10, 0, 0, True))
    def test_zero_speed_does_not_move(self):
        self.assertEqual(step(-10, 10, 2, 0, True), (0, False))
    def test_positive_step_and_clamp(self):
        self.assertEqual(step(0, -3, 4, 5, False), (6, True))
    def test_negative_step_and_clamp(self):
        self.assertEqual(step(0, 4, 3, 5, True), (-8, True))
    def test_subthreshold_not_reached(self):
        self.assertEqual(step(0, -10, 2, 4, False), (8, False))
    def test_already_at_bound(self):
        self.assertEqual(step(10, -5, 5, 100, False), (0, True))
    def test_low16_wrap(self):
        self.assertEqual(step(0, -2, 200, 255, False), (s16(51000), False))

if __name__ == '__main__':
    unittest.main(verbosity=2)
