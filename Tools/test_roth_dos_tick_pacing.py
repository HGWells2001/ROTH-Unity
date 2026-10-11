#!/usr/bin/env python3
"""Deterministic Python model of opt-in PIT pacing (NOT C# compilation)."""
import math
import unittest

PIT_HZ=1193182.0
DIVISOR=0x4216
TICK_HZ=PIT_HZ/DIVISOR

def simulate_frame(fraction,counter,seconds,max_ticks=45):
    if not math.isfinite(seconds) or not 0<=seconds<=3600:
        return (fraction,counter,0,0)
    exact=fraction+seconds*TICK_HZ
    whole=int(math.floor(exact))
    fraction=exact-whole
    counter=(counter+whole)&0xffff
    return fraction,counter,whole,min(max(0,max_ticks),whole))

def move_toward(current,target,ticks,speed):
    assert 0<=speed<=255
    inc=ticks*speed
    return min(current+inc,target) if current<target else max(current-inc,target)

class DosPacingTests(unittest.TestCase):
    def test_pit_divisor(self):self.assertEqual(DIVISOR,16918)
    def test_nominal_frequency(self):self.assertAlmostEqual(TICK_HZ,70.5273673,places=5)
    def test_exact_one_tick(self):
        self.assertEqual(simulate_frame(0,0,1/TICK_HZ)[2:],(1,1))
    def test_two_half_ticks(self):
        a=simulate_frame(0,0,.5/TICK_HZ)
        b=simulate_frame(a[0],a[1],.5/TICK_HZ)
        self.assertEqual(a[2],0);self.assertEqual((b[1],b[2]),(1,1))
    def test_modulo_counter_wrap(self):
        a=simulate_frame(0,65535,1/TICK_HZ)
        self.assertEqual(a[1],0)
    def test_game_loop_safety_cap(self):
        a=simulate_frame(0,0,60/TICK_HZ,max_ticks=45)
        self.assertEqual((a[2],a[3]),(60,45))
    def test_60fps_one_second_average(self):
        f=0;c=0;total=0
        for i in range(60):
            f,c,dt,step=simulate_frame(f,c,1/60)
            total+=dt
        self.assertEqual(total,70)
    def test_zero_seconds(self):self.assertEqual(simulate_frame(.25,10,0),(.25,10,0,0))
    def test_nonfinite_rejected(self):
        for s in [-1,float('nan'),float('inf')]:
            self.assertEqual(simulate_frame(.4,3,s),(.4,3,0,0))
    def test_signed_speed_byte_zero_does_not_move(self):self.assertEqual(move_toward(0,20,5,0),0)
    def test_signed_speed_byte_positive(self):self.assertEqual(move_toward(0,20,2,3),6)
    def test_step_clamped_to_target(self):self.assertEqual(move_toward(0,5,4,255),5)
    def test_return_direction(self):self.assertEqual(move_toward(20,0,2,3),14)
    def test_accumulation_invariant_short_vs_long(self):
        a=simulate_frame(0,0,2/TICK_HZ)
        b=simulate_frame(0,0,1/TICK_HZ)
        b=simulate_frame(b[0],b[1],1/TICK_HZ)
        self.assertEqual((a[1],a[2]),(b[1],2))

if __name__=='__main__':unittest.main(verbosity=2)
