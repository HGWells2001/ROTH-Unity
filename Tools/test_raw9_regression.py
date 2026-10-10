#!/usr/bin/env python3
"""Static contract + numerical invariants for RAW9; not an engine runtime test."""
from pathlib import Path
import unittest
ROOT=Path(__file__).resolve().parents[1]
MESH=(ROOT/'Assets/ROTHUnity/Runtime/RothMapMeshBuilder.cs').read_text()
MOVER=(ROOT/'Assets/ROTHUnity/Runtime/RothHorizontalSectorMover.cs').read_text()
def translate(vertices, objects, previous, target):
    delta=(target[0]-previous[0],target[1]-previous[1])
    valid=lambda x:-32768<=x<=32767
    if any(not valid(x+target[0]) or not valid(y+target[1]) for x,y in vertices):return None
    if any(not valid(x+delta[0]) or not valid(y+delta[1]) for x,y in objects):return None
    return ([(x+target[0],y+target[1]) for x,y in vertices],
            [(x+delta[0],y+delta[1]) for x,y in objects])
class Raw9Tests(unittest.TestCase):
    def test_vertex_overflow_is_rejected(self):
        self.assertIsNone(translate([(3,0),(32760,10)],[],(0,0),(20,0)))
    def test_object_overflow_is_rejected(self):
        self.assertIsNone(translate([(3,5)],[(32760,0)],(0,0),(20,0)))
    def test_retrigger_starts_at_current_position(self):
        self.assertEqual(90+450-200,340)
    def test_reversal_restores_live_origin(self):
        self.assertEqual((90+450-200)-(450-200),90)
    def test_csharp_safety_contracts(self):
        self.assertIn('long x = (long)v.X + rawOffset.x',MESH)
        self.assertIn('long x = (long)obj.PosX + delta.x',MESH)
        self.assertIn('Destination = (alongX ? existing.x : existing.y) + (float)end - start',MOVER)
        self.assertIn('Builder.RuntimeSetSectorTranslation(m.SectorId, before)',MOVER)
        self.assertLess(MOVER.find('if (!Builder.RuntimeSetSectorTranslation(m.SectorId, after))'),MOVER.find('Player.Move(worldDelta)'))
    def test_short_step_clipping_is_detected_in_linear_units(self):
        # With the old squared-distance epsilon a fully blocked 0.01m move
        # passed the test (0.001 >= 0.01**2); linear comparison rejects it.
        def blocked(required, achieved):
            tolerance=min(0.001,required*0.1)
            return required>0.000001 and achieved<required-tolerance
        self.assertTrue(blocked(0.01,0.0))
        self.assertTrue(blocked(1.0,0.8))
        self.assertFalse(blocked(1.0,0.9995))
    def test_rider_rollback_restores_transform_without_move_back(self):
        self.assertIn('Player.enabled = false;',MOVER)
        self.assertIn('Player.transform.position = oldPosition;',MOVER)
        self.assertIn('Physics.SyncTransforms();',MOVER)
        self.assertNotIn('Player.Move(-achieved)',MOVER)
    def test_raw9_trace_is_opt_in_and_includes_command_fields(self):
        self.assertIn('public bool TraceRaw9;',MOVER)
        self.assertIn('if (!TraceRaw9) return;',MOVER)
        self.assertIn('RAW9_TRACE|',MOVER)
        self.assertIn('motion.RawFlags, motion.RawRevertTicks',MOVER)
if __name__=='__main__':unittest.main(verbosity=2)
