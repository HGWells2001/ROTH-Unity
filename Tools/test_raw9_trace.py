#!/usr/bin/env python3
"""Offline regression checks for RAW9 trace parsing."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location(
    "raw9_trace", Path(__file__).with_name("analyze_raw9_trace.py"))
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

def record(event="step", sector=12, time=1.0, before=0, after=10):
    values = [100, time, sector, "X", event, before, 0, after, 0,
              100, 110, after, 10.0, 128.0, 1.0, 2.0, 3.0, 64, 8]
    return "prefix RAW9_TRACE|" + "|".join(map(str, values))

class TraceTests(unittest.TestCase):
    def test_parse_step(self):
        value = mod.parse_line(record(), 1)
        self.assertEqual((value["sector"], value["after_x"]), (12, 10))
    def test_ignore_noise(self):
        self.assertIsNone(mod.parse_line("hello", 1))
    def test_distance(self):
        report = mod.analyze("\n".join([record("trigger", time=0, after=0),
                                          record("step", time=0.1, before=0, after=10),
                                          record("arrived", time=0.2, before=10, after=10)]))
        self.assertEqual(report["sectors"]["12"]["total_committed_raw_distance"], 10)
        self.assertEqual(report["malformed_rows"], [])
    def test_malformed_does_not_hide_good_rows(self):
        report = mod.analyze(record() + "\nRAW9_TRACE|junk\n")
        self.assertEqual(report["trace_rows"], 1)
        self.assertEqual(len(report["malformed_rows"]), 1)
    def test_detect_rollback_warning(self):
        report = mod.analyze(record("rollback-failed"))
        self.assertIn("sector rollback failed", report["sectors"]["12"]["warnings"])
    def test_reject_nan(self):
        entry = record().replace("|128.0|", "|nan|")
        with self.assertRaisesRegex(ValueError, "not finite"):
            mod.parse_line(entry, 1)
    def test_sectors_independent(self):
        report = mod.analyze(record(sector=1) + "\n" + record(sector=2))
        self.assertEqual(set(report["sectors"]), {"1", "2"})
    def test_negative_direction_absolute_distance(self):
        report = mod.analyze(record(before=10, after=3))
        self.assertEqual(report["sectors"]["12"]["total_committed_raw_distance"], 7)

if __name__ == "__main__":
    unittest.main(verbosity=2)
