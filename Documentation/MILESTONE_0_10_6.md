# Milestone 0.10.6: deterministic sector-door timing

## Changes
- Auto-close countdown begins upon **reaching the open position**, not when motion starts.
- Re-triggering a moving sector retains its actual current height rather than teleporting to RAW start height.
- Final geometry/collision height is always committed even if the mesh-refresh throttle has not elapsed.
- Obstruction while closing resets the hold until the reopen position is reached again.

## Remaining limitations
- Experimental timing: auto-close tick duration and motion speed are not retail-verified.
- The occupancy test uses the horizontal sector polygon and player capsule radius; vertical clearance and platform carry are not yet modelled.
- Unity compilation and in-game tests have not been performed in this environment.
- No retail game assets are included.
