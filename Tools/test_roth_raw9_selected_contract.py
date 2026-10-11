#!/usr/bin/env python3
"""C# source contracts for opt-in RAW9 mode; NOT a Unity compile/test."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1] / 'Assets/ROTHUnity/Runtime'
BUILDER = ROOT / 'RothMapMeshBuilder.cs'
BACKEND = ROOT / 'RothMapMeshBuilder.Raw9Selected.cs'
MOVER = ROOT / 'RothHorizontalSectorMover.cs'


class SelectedBackendSourceContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.builder = BUILDER.read_text(encoding='utf-8')
        cls.backend = BACKEND.read_text(encoding='utf-8')
        cls.mover = MOVER.read_text(encoding='utf-8')

    def test_partial_builder_is_consistent(self):
        self.assertIn('public sealed partial class RothMapMeshBuilder', self.builder)
        self.assertIn('public sealed partial class RothMapMeshBuilder', self.backend)

    def test_opt_in_has_no_default_true(self):
        self.assertIn('public bool UseSelectedRaw9Vertices;', self.mover)
        self.assertNotIn('UseSelectedRaw9Vertices = true', self.mover)

    def test_selection_is_only_four_raw_vertices(self):
        self.assertIn('RothRaw9VertexInspector.TrySelect(_map, sectorId, alongX', self.backend)
        self.assertIn('new RothVertex[4]', self.backend)
        self.assertIn('for (int i = 0; i < 4; i++)', self.backend)
        self.assertNotIn('_map.Vertices.Add(', self.backend)

    def test_topology_findings_refuse_start(self):
        self.assertIn('RothRaw9TopologyInspector.Inspect(_map, selection)', self.backend)
        self.assertIn('if (topo.HasStaticHazards)', self.backend)

    def test_closed_face_ring_is_required(self):
        self.assertIn('if (sector.FacesCount != 4)', self.backend)
        self.assertIn('if (face.VertexIndex02 != next.VertexIndex01)', self.backend)

    def test_backend_is_not_mixed(self):
        self.assertIn('if (_movingSectorVertices.Count != 0)', self.backend)
        self.assertIn('if (_map == null || _raw9SelectedSectorMotions.Count != 0)', self.builder)
        self.assertIn('Builder.RuntimeIsSelectedRaw9MotionActive(sectorId)', self.mover)

    def test_axis_is_immutable_per_sector(self):
        self.assertIn('existing.AlongX == alongX', self.backend)
        self.assertIn('if ((alongX && offset.y != 0) || (!alongX && offset.x != 0))', self.backend)

    def test_16bit_vertex_and_object_bounds_checked_first(self):
        self.assertIn('if (x < short.MinValue || x > short.MaxValue ||', self.backend)
        self.assertIn('y < short.MinValue || y > short.MaxValue)', self.backend)
        self.assertIn('foreach (RothObject obj in _map.Objects)', self.backend)

    def test_source_vertex_state_must_not_be_stale(self):
        self.assertIn('if ((long)original.X + state.Offset.x != current.X ||', self.backend)
        self.assertIn('Refuse to overwrite an externally mutated vertex', self.backend)

    def test_geometry_and_collision_rebuilt(self):
        self.assertIn('RefreshRuntimeGeometry(rebuildObjects: true)', self.backend)
        self.assertIn('collider.sharedMesh = mesh;', self.builder)

    def test_failed_refresh_rolls_back_in_memory_changes(self):
        self.assertIn('catch (Exception error)', self.backend)
        self.assertIn('state.Offset = before;', self.backend)
        self.assertIn('obj.PosX = (short)(obj.PosX - dx);', self.backend)

    def test_mode_frozen_per_running_motion(self):
        self.assertIn('SelectedGeometry = selected', self.mover)
        self.assertIn('m.SelectedGeometry', self.mover)
        self.assertIn('Builder.RuntimeSetSelectedRaw9Translation', self.mover)
        self.assertIn('Builder.RuntimeSetSectorTranslation', self.mover)

    def test_mode_is_used_for_player_collision_rollback(self):
        self.assertIn('bool restored = m.SelectedGeometry', self.mover)
        self.assertIn('Builder.RuntimeSetSelectedRaw9Translation(m.SectorId, m.AlongX, before)', self.mover)

    def test_map_reload_cancels_outstanding_motion(self):
        self.assertIn('unchecked { _raw9MapGeneration++; }', self.builder)
        self.assertIn('MapGeneration = Builder.RuntimeMapGeneration', self.mover)
        self.assertIn('m.MapGeneration != Builder.RuntimeMapGeneration', self.mover)


if __name__ == '__main__':
    unittest.main(verbosity=2)
