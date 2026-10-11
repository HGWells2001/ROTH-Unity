#!/usr/bin/env python3
"""Static C# contracts, not Unity compilation or live physics tests."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / 'Assets/ROTHUnity/Core/RothRaw9TopologyInspector.cs'
EDITOR = ROOT / 'Assets/ROTHUnity/Editor/RothRaw9VertexAuditMenu.cs'
MOVER = ROOT / 'Assets/ROTHUnity/Runtime/RothHorizontalSectorMover.cs'

class TopologyContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.core = CORE.read_text(encoding='utf-8')
        cls.editor = EDITOR.read_text(encoding='utf-8')
    def test_shared_indices_across_sectors_are_detected(self):
        self.assertIn('face.SectorIndex != sectorIndex', self.core)
        self.assertIn('shared_vertex_outside_sector', self.core)
    def test_portal_and_coincident_warnings(self):
        for term in ('face.SisterFaceIndex', 'portal_edge_with_moving_vertex',
                     'coincident_external_vertex', 'invalid_sister_face'):
            self.assertIn(term, self.core)
    def test_editor_is_wired_to_read_only_audit(self):
        self.assertIn('RothRaw9TopologyInspector.Inspect(map, selected)', self.editor)
        self.assertIn('TOPOLOGY REVIEW:', self.editor)
    def test_runtime_remains_unmodified(self):
        mover = MOVER.read_text(encoding='utf-8')
        self.assertNotIn('RothRaw9TopologyInspector.Inspect', mover)
        self.assertNotIn('RothDosRaw9Reference.ComputeAxisStep', mover)

if __name__ == '__main__':
    unittest.main(verbosity=2)
