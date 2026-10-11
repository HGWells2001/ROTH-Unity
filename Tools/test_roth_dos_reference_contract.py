#!/usr/bin/env python3
"""Static checks only: NOT C# compile/run tests."""
from pathlib import Path
import unittest

SOURCE = Path(__file__).resolve().parents[1] / 'Assets/ROTHUnity/Runtime/RothDosRaw9Reference.cs'

class CSharpReferenceContract(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.code = SOURCE.read_text(encoding='utf-8')
    def test_nominal_pit_divisors(self):
        self.assertIn('InitialPitDivisor = 0x4242;', self.code)
        self.assertIn('RunningPitDivisor = 0x4216;', self.code)
    def test_nominal_hardware_hertz(self):
        self.assertIn('PitInputHz = 1193182.0;', self.code)
    def test_signed_extended_counter_delta(self):
        self.assertIn('return (short)now - (short)previous;', self.code)
    def test_signed_raw_bounds_wrap(self):
        self.assertIn('unchecked((short)(-2 * (int)signedCommandBound))', self.code)
    def test_direction_and_16bit_steps(self):
        self.assertIn('if (reverse) signedSpeed = unchecked(-signedSpeed);', self.code)
        self.assertIn('short rawDelta = unchecked((short)signedSpeed);', self.code)
        self.assertIn('short candidate = unchecked((short)(currentCoordinate + rawDelta));', self.code)
        self.assertIn('bool clamp = reverse ? candidate < target : candidate > target;', self.code)
    def test_not_wired_into_unverified_mover(self):
        # A new isolated implementation should not silently replace experimental gameplay.
        mover = SOURCE.with_name('RothHorizontalSectorMover.cs')
        if mover.exists():
            self.assertNotIn('RothDosRaw9Reference.ComputeAxisStep', mover.read_text())

if __name__ == '__main__':unittest.main(verbosity=2)
