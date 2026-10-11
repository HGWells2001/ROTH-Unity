#!/usr/bin/env python3
"""Synthetic event-log integration tests (no Unity Editor required)."""
import unittest
from audit_roth_raw9_playmode import inspect, parse_pit, parse_state


def pit(frame=1, ticks=1, elapsed=None):
    if elapsed is None:
        elapsed=ticks
    return f"ROTH_PIT_TRACE|{frame}|1.000|1|{elapsed}|{ticks}|0.500"


def state(event="trigger", flags=0, sector=5, frame=1):
    return f"RAW9_DOS_STATE|{frame}|4|{sector}|{event}|34|{flags}|0|1"


def move(before=0, after=5, frame=1, flags=5 << 8, axis="X", sector=5, event="step"):
    cols = [frame, 1.0, sector, axis, event, before, 0, after, 0, -10, 10, after,
            50.0, 5.0, 0.0, 0.0, 0.0, flags, 2]
    return "RAW9_TRACE|" + "|".join(map(str, cols))


class PlayModeAuditTests(unittest.TestCase):
    def test_read_pit(self):
        self.assertEqual(parse_pit(pit(), 1)["pacing_ticks"], 1)

    def test_read_state(self):
        self.assertEqual(parse_state(state(), 1)["command_state"], 34)

    def test_clean_forward(self):
        x=inspect("\n".join([pit(),state(),move()]))
        self.assertEqual((x["motion_rows"],x["pit_rows"],x["state_rows"]),(1,1,1))
        self.assertEqual(x["warnings"], [])

    def test_direction_error_forward(self):
        x=inspect("\n".join([pit(),state(),move(before=5,after=0)]))
        self.assertTrue(any("opposes" in w for w in x["warnings"]))

    def test_direction_reverse_pass(self):
        x=inspect("\n".join([pit(),state(flags=128),move(before=5,after=0)]))
        self.assertEqual(x["warnings"], [])

    def test_excess_distance(self):
        x=inspect("\n".join([pit(),state(),move(after=6)]))
        self.assertTrue(any("allowance" in w for w in x["warnings"]))

    def test_waiting_motion_error(self):
        x=inspect("\n".join([pit(),state(flags=64),move()]))
        self.assertTrue(any("during DOS wait" in w for w in x["warnings"]))

    def test_missing_pit(self):
        x=inspect("\n".join([state(),move()]))
        self.assertTrue(any("missing PIT" in w for w in x["warnings"]))

    def test_zero_ticks_does_not_allow_step(self):
        x=inspect("\n".join([pit(ticks=0),state(),move()]))
        self.assertTrue(any("allowance is 0" in w for w in x["warnings"]))

    def test_duplicate_pit_warning(self):
        x=inspect("\n".join([pit(),pit(),state(),move()]))
        self.assertTrue(any("duplicate PIT" in w for w in x["warnings"]))

    def test_no_false_alarm_for_legacy_without_state(self):
        x=inspect(move(after=200))
        self.assertEqual(x["warnings"], [])
        self.assertEqual(x["sectors"]["5"]["examined_steps"], 0)

    def test_malformed_state_line(self):
        x=inspect("RAW9_DOS_STATE|hello")
        self.assertEqual(len(x["parse_errors"]), 1)

    def test_malformed_pit_line(self):
        x=inspect("ROTH_PIT_TRACE|1|nope|1|1|1|0.5")
        self.assertEqual(len(x["parse_errors"]), 1)

    def test_no_inherited_state_after_arrived(self):
        x=inspect("\n".join([pit(),state(),state("arrived"),move(after=20)]))
        self.assertEqual(x["sectors"]["5"]["examined_steps"], 0)

    def test_independent_sectors(self):
        x=inspect("\n".join([pit(),state(),move(),move(sector=6,after=50)]))
        self.assertEqual(x["sectors"]["6"]["examined_steps"], 0)

    def test_parser_rejects_invalid_pit_fraction(self):
        x=inspect("ROTH_PIT_TRACE|1|1|0|1|1|1.5")
        self.assertEqual(len(x["parse_errors"]), 1)

    def test_nonmoving_axis_guard(self):
        line=move().split("|")
        line[7]="8"
        line[8]="2"
        x=inspect("\n".join([pit(),state(),"|".join(line)]))
        self.assertTrue(any("non-moving axis" in w for w in x["warnings"]))


if __name__ == "__main__":
    unittest.main(verbosity=2)
