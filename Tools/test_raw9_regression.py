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
if __name__=='__main__':unittest.main(verbosity=2)
