using System.Collections.Generic;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// RAW opcode 9 sector translation. Numeric timing and relative-offset interpretation
    /// are experimental until verified against the retail game.
    /// </summary>
    public sealed class RothHorizontalSectorMover : MonoBehaviour
    {
        public RothMapMeshBuilder Builder;
        public CharacterController Player;
        public float RawUnitsPerSecond = 128f;
        [Tooltip("Speed per high-byte flag unit; provisional, compare against retail gameplay")]
        public float RawSpeedPerFlagUnit = 32f;
        public float AutoRevertTickSeconds = 0.1f;
        public bool CarryPlayer = true;
        public bool AvoidPlayerObstruction = true;
        [Tooltip("Emit opt-in RAW9_TRACE lines to the Unity Console for DOS timing comparisons.")]
        public bool TraceRaw9;
        [Tooltip("EXPERIMENTAL: only four DOS-selected vertex coordinate words are moved. Off by default. Refuses unsafe shared geometry and mixed-mode sectors.")]
        public bool UseSelectedRaw9Vertices;
        [Tooltip("EXPERIMENTAL: use nominal DOS IRQ0 integer ticks x command speed byte, instead of Time.deltaTime. Requires selected RAW9 geometry. OFF by default; motion bounds and return states remain experimental.")]
        public bool UseNominalDosTickPacing;
        [Tooltip("Shared per-frame DOS tick source. An instance is created on this GameObject when the optional pacing mode is first triggered.")]
        public RothDosTickClock DosTickClock;
        [Tooltip("EXPERIMENTAL: original DOS absolute coordinates. Requires selected geometry and PIT pacing. Without the separate DOS state machine checkbox this is restricted to one-way, zero-timeout motion. OFF by default.")]
        public bool UseDosSingleShotAbsoluteBounds;
        [Tooltip("Manual initial direction for direct/one-shot calls. The experimental DOS state-machine mode reads original command+2 from the RAW command parser instead when an indexed command triggers it.")]
        public bool DosInitialCommandStateBit02;
        [Tooltip("EXPERIMENTAL original RAW9 animation-state transitions: dynamic command bit 0x02, active retrigger, signed tick timeout and delayed reversal. Requires the three DOS experiment switches above. OFF by default.")]
        public bool UseDosCommandStateTransitions;

        private sealed class Motion
        {
            public ushort SectorId;
            public bool AlongX;
            public bool SelectedGeometry;
            public bool DosTickPacing;
            public bool DosOneWayBounds;
            public RothDosRaw9CommandState DosState;
            public int RawCommandIndex;
            public bool DosReverse;
            public short DosBoundWord;
            public int MapGeneration;
            public float Position;
            public float Destination;
            public float ReturnPosition;
            public float OutwardDestination;
            public float Speed;
            public float ReturnDelay;
            public float ReturnAt = -1f;
            public bool Returning;
            public bool Repeat;
            public short RawStart;
            public short RawEnd;
            public ushort RawFlags;
            public ushort RawRevertTicks;
        }
        private readonly List<Motion> _motions = new List<Motion>();
        // Per-command dynamic modifier; sector ID alone cannot identify the
        // triggering RAW command when a room contains multiple command chains.
        private readonly Dictionary<int, RothDosRaw9CommandState> _dosCommandStates =
            new Dictionary<int, RothDosRaw9CommandState>();
        private int _dosCommandStateGeneration = -1;

        public void CancelAll()
        {
            _motions.Clear();
            _dosCommandStates.Clear();
            _dosCommandStateGeneration = -1;
        }

        // Pipe-separated, invariant-culture records: frame, time (scaled seconds),
        // sector, axis, event, previous/next RAW X/Y, command start/end,
        // interpolated position/destination, speed, player XYZ, flags, timeout ticks.
        private void Trace(Motion motion, string action, Vector2Int before, Vector2Int after)
        {
            if (!TraceRaw9) return;
            Vector3 playerPosition = Player != null ? Player.transform.position : Vector3.zero;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "RAW9_TRACE|{0}|{1:F6}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11:F4}|{12:F4}|{13:F4}|{14:F4}|{15:F4}|{16:F4}|{17}|{18}",
                Time.frameCount, Time.time, motion.SectorId,
                motion.AlongX ? "X" : "Y", action, before.x, before.y,
                after.x, after.y, motion.RawStart, motion.RawEnd,
                motion.Position, motion.Destination, motion.Speed,
                playerPosition.x, playerPosition.y, playerPosition.z,
                motion.RawFlags, motion.RawRevertTicks), this);
        }

        // Extra state instrumentation deliberately leaves RAW9_TRACE intact.
        // frame, command index, sector, event, modifier byte, animation flags,
        // unsigned 16-bit wait ticks, active bit. For DOSBox A/B captures.
        private void TraceDosState(Motion motion, string action)
        {
            if (!TraceRaw9 || motion.DosState == null) return;
            RothDosRaw9CommandState state = motion.DosState;
            Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "RAW9_DOS_STATE|{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
                Time.frameCount, motion.RawCommandIndex, motion.SectorId,
                action, state.CommandState, state.AnimationFlags,
                state.WaitTicks, state.Active ? 1 : 0), this);
        }

        // Trace-only: the active goal changes whenever the DOS state toggles
        // direction. The actual geometry update always uses the current
        // original vertex extremum, NOT this interpolated float destination.
        private void RefreshDosGoalForTrace(Motion motion)
        {
            if (Builder == null || motion.DosState == null) return;
            short minimum, maximum;
            Vector2Int offset;
            if (!Builder.RuntimeGetSectorTranslation(motion.SectorId, out offset) ||
                !Builder.RuntimeTryReadRaw9SelectedRange(motion.SectorId,
                    motion.AlongX, out minimum, out maximum)) return;
            int destination;
            short target;
            string reason;
            if (RothDosRaw9Bounds.TryPlan(minimum, maximum,
                motion.RawStart, motion.RawEnd, motion.DosReverse,
                motion.AlongX ? offset.x : offset.y,
                out destination, out target, out reason))
                motion.Destination = destination;
        }

        public bool Move(ushort sectorId, bool alongX, short start, short end,
            ushort revertTicks, bool repeat, ushort flags = 0,
            int rawCommandIndex = 0, byte rawCommandModifier = 0)
        {
            if (Builder == null) Builder = GetComponent<RothMapMeshBuilder>();
            if (Builder == null) return false;
            Vector2Int existing;
            if (!Builder.RuntimeGetSectorTranslation(sectorId, out existing)) return false;
            bool selected = UseSelectedRaw9Vertices;
            if (UseNominalDosTickPacing && !selected)
            {
                Debug.LogWarning("RAW9 nominal DOS tick pacing requires selected four-vertex geometry.", this);
                return false;
            }
            bool tickPacing = selected && UseNominalDosTickPacing;
            bool dosBounds = UseDosSingleShotAbsoluteBounds;
            bool dosStates = UseDosCommandStateTransitions;
            if (dosStates && (!dosBounds || !tickPacing))
            {
                Debug.LogWarning("RAW9 DOS command states require both selected geometry, PIT pacing and absolute bounds.", this);
                return false;
            }
            if (dosBounds && (!tickPacing || (!dosStates && (repeat || revertTicks != 0))))
            {
                Debug.LogWarning("RAW9 DOS bounds require PIT timing; timeout and repeat require the opt-in DOS state machine.", this);
                return false;
            }

            // Map rebuilds invalidate all command indices and their dynamic
            // modifier bytes. Never apply command state to a different map.
            if (_dosCommandStateGeneration != Builder.RuntimeMapGeneration)
            {
                _dosCommandStates.Clear();
                _dosCommandStateGeneration = Builder.RuntimeMapGeneration;
            }

            RothDosRaw9CommandState dosState = null;
            if (dosBounds)
            {
                for (int i = 0; i < _motions.Count; i++)
                {
                    Motion active = _motions[i];
                    if (active.SectorId != sectorId) continue;
                    if (dosStates && active.DosState != null &&
                        rawCommandIndex > 0 && active.RawCommandIndex == rawCommandIndex)
                    {
                        // DOS obj1+0x22BB1: only a running repeatable command
                        // without a timeout permits direction reversal here.
                        if (active.DosState.TryActiveRetrigger(repeat, revertTicks))
                        {
                            active.DosReverse = active.DosState.Reverse;
                            active.DosBoundWord = active.DosReverse ? active.RawEnd : active.RawStart;
                            RefreshDosGoalForTrace(active);
                            Vector2Int live;
                            if (Builder.RuntimeGetSectorTranslation(sectorId, out live))
                                Trace(active, "retrigger", live, live);
                            TraceDosState(active, "retrigger");
                            return true;
                        }
                    }
                    Debug.LogWarning("RAW9 DOS bounds refused unsupported active command or another motion in this sector.", this);
                    return false;
                }
            }
            if (dosStates)
            {
                RothDosRaw9CommandState saved;
                byte modifier = rawCommandIndex > 0 &&
                    _dosCommandStates.TryGetValue(rawCommandIndex, out saved)
                    ? saved.CommandState
                    : rawCommandIndex > 0 ? rawCommandModifier :
                      (byte)(DosInitialCommandStateBit02 ? 0x02 : 0);
                // A DOS command disabled by completion must not be silently
                // re-enabled by a new Unity chain trigger.
                if ((modifier & 0x08) != 0)
                {
                    Debug.LogWarning("RAW9 DOS command state rejects completed/disabled command.", this);
                    return false;
                }
                dosState = new RothDosRaw9CommandState(modifier);
            }
            bool dosReverse = dosStates ? dosState.Reverse : !DosInitialCommandStateBit02;
            int plannedDosOffset = 0;
            if (dosBounds)
            {
                short minimum, maximum;
                if (!Builder.RuntimeTryReadRaw9SelectedRange(sectorId, alongX, out minimum, out maximum))
                {
                    Debug.LogWarning("RAW9 DOS bounds rejected: original vertex coordinate range unavailable.", this);
                    return false;
                }
                short rawTarget;
                string reason;
                if (!RothDosRaw9Bounds.TryPlan(minimum, maximum, start, end,
                    dosReverse, alongX ? existing.x : existing.y,
                    out plannedDosOffset, out rawTarget, out reason))
                {
                    Debug.LogWarning("RAW9 DOS bounds rejected sector " + sectorId + ": " + reason, this);
                    return false;
                }
                if (TraceRaw9)
                    Debug.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "RAW9_DOS_BOUNDS|{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                        sectorId, alongX ? "X" : "Y", dosReverse ? "reverse" : "forward",
                        minimum, maximum, rawTarget, plannedDosOffset), this);
            }
            if (tickPacing && DosTickClock == null)
            {
                DosTickClock = GetComponent<RothDosTickClock>();
                if (DosTickClock == null) DosTickClock = gameObject.AddComponent<RothDosTickClock>();
            }
            if (selected)
            {
                string reason;
                if (!Builder.RuntimeTryPrepareSelectedRaw9Motion(sectorId, alongX, out reason))
                {
                    Debug.LogWarning("RAW9 selected-vertex mode rejected sector " + sectorId + ": " + reason, this);
                    return false;
                }
            }
            else if (Builder.RuntimeIsSelectedRaw9MotionActive(sectorId))
            {
                Debug.LogWarning("RAW9 sector " + sectorId + " is assigned to the selected-vertex mode until the map rebuilds.", this);
                return false;
            }
            Builder.RuntimeSetSectorTextureFollowFlags(sectorId, flags);
            for (int i = _motions.Count - 1; i >= 0; --i)
                if (_motions[i].SectorId == sectorId) _motions.RemoveAt(i);
            Motion motion = new Motion {
                SectorId = sectorId, AlongX = alongX,
                SelectedGeometry = selected,
                DosTickPacing = tickPacing,
                DosOneWayBounds = dosBounds,
                DosState = dosState,
                RawCommandIndex = rawCommandIndex,
                DosReverse = dosReverse,
                DosBoundWord = dosReverse ? end : start,
                MapGeneration = Builder.RuntimeMapGeneration,
                Position = alongX ? existing.x : existing.y,
                // End-start remains an unverified RAW displacement.
                // Re-triggers now begin at the live in-flight sector position.
                Destination = dosBounds ? plannedDosOffset :
                    (alongX ? existing.x : existing.y) + (float)end - start,
                OutwardDestination = dosBounds ? plannedDosOffset :
                    (alongX ? existing.x : existing.y) + (float)end - start,
                ReturnPosition = alongX ? existing.x : existing.y,
                // In the opt-in clock mode, the original unsigned command[7]
                // byte is multiplied by whole PIT ticks. The old continuous
                // prototype speed remains unchanged when this option is off.
                Speed = tickPacing ? (float)((flags >> 8) & 255) :
                    Mathf.Max(1f, ((flags >> 8) & 255) == 0 ? RawUnitsPerSecond :
                        ((flags >> 8) & 255) * Mathf.Max(1f, RawSpeedPerFlagUnit)),
                ReturnDelay = revertTicks * Mathf.Max(0f, AutoRevertTickSeconds),
                Repeat = repeat,
                RawStart = start, RawEnd = end,
                RawFlags = flags, RawRevertTicks = revertTicks
            };
            _motions.Add(motion);
            if (dosStates && rawCommandIndex > 0)
                _dosCommandStates[rawCommandIndex] = dosState;
            Trace(motion, "trigger", existing, existing);
            TraceDosState(motion, "trigger");
            return true;
        }

        private void Update()
        {
            if (Builder == null) return;
            if (Player == null && CarryPlayer)
            {
                RothFirstPersonController fp = FindFirstObjectByType<RothFirstPersonController>();
                if (fp != null) Player = fp.GetComponent<CharacterController>();
            }
            for (int i = _motions.Count - 1; i >= 0; --i)
            {
                Motion m = _motions[i];
                if (m.MapGeneration != Builder.RuntimeMapGeneration)
                {
                    _motions.RemoveAt(i);
                    continue;
                }
                Vector2Int before;
                if (!Builder.RuntimeGetSectorTranslation(m.SectorId, out before))
                {
                    _motions.RemoveAt(i);
                    continue;
                }
                if (m.DosTickPacing && (DosTickClock == null || !DosTickClock.isActiveAndEnabled))
                {
                    // Do not advance from a stale per-frame tick value or
                    // silently switch timebase when the clock is unavailable.
                    Debug.LogWarning("RAW9 tick-paced motion canceled: DOS clock is missing or disabled.", this);
                    _motions.RemoveAt(i);
                    continue;
                }
                float next;
                RothDosRaw9Reference.AxisStep originalStep =
                    default(RothDosRaw9Reference.AxisStep);
                if (m.DosOneWayBounds)
                {
                    if (m.DosState != null)
                    {
                        // A waiting update consumes the signed DOS word timer
                        // but does NOT move vertices until the next update.
                        if (m.DosState.ConsumeWait(DosTickClock.PacingTicksThisFrame))
                        {
                            if (!m.DosState.Waiting)
                                TraceDosState(m, "wait-finished");
                            Trace(m, m.DosState.Waiting ? "wait" : "wait-finished", before, before);
                            continue;
                        }
                        m.DosReverse = m.DosState.Reverse;
                        m.DosBoundWord = m.DosReverse ? m.RawEnd : m.RawStart;
                        // Retain Unity's conservative player-obstruction gate
                        // on an automatic return. It is not DOS-proven.
                        if (AvoidPlayerObstruction && (m.DosState.AnimationFlags & 0x20) != 0 &&
                            Player != null && Builder.RuntimePlayerOccupiesSector(m.SectorId, Player))
                        {
                            Trace(m, "return-obstructed", before, before);
                            continue;
                        }
                    }
                    short minimum, maximum;
                    if (!Builder.RuntimeTryReadRaw9SelectedRange(m.SectorId, m.AlongX,
                        out minimum, out maximum))
                    {
                        Debug.LogWarning("RAW9 DOS bounds motion canceled: selected coordinates changed externally.", this);
                        _motions.RemoveAt(i);
                        continue;
                    }
                    short current = m.DosReverse ? minimum : maximum;
                    originalStep = RothDosRaw9Reference.ComputeAxisStep(
                        current, m.DosBoundWord, DosTickClock.PacingTicksThisFrame,
                        (byte)((m.RawFlags >> 8) & 255), m.DosReverse);
                    next = m.Position + originalStep.Delta;
                }
                else
                {
                    float frameStep = m.DosTickPacing
                        ? m.Speed * DosTickClock.PacingTicksThisFrame
                        : m.Speed * Time.deltaTime;
                    next = Mathf.MoveTowards(m.Position, m.Destination, frameStep);
                }
                Vector2Int after = before;
                if (m.AlongX) after.x = Mathf.RoundToInt(next);
                else after.y = Mathf.RoundToInt(next);
                Vector2Int delta = after - before;
                if (delta != Vector2Int.zero)
                {
                    short sectorFloor;
                    bool hasFloor = Builder.RuntimeGetSectorHeight(m.SectorId, false, out sectorFloor);
                    bool onPlatform = CarryPlayer && Player != null && hasFloor &&
                        Builder.RuntimePlayerStandsOnFloor(m.SectorId, Player, sectorFloor);
                    Vector3 localDelta = new Vector3(
                        (Builder.MirrorWorldX ? -delta.x : delta.x) * Builder.CoordinateScale,
                        0f, delta.y * Builder.CoordinateScale);
                    Vector3 worldDelta = Builder.transform.TransformVector(localDelta);
                    // Commit geometry first. Failed vertex/object bounds checks
                    // must not move the player.
                    bool committed = m.SelectedGeometry
                        ? Builder.RuntimeSetSelectedRaw9Translation(m.SectorId, m.AlongX, after)
                        : Builder.RuntimeSetSectorTranslation(m.SectorId, after);
                    if (!committed)
                    {
                        Trace(m, "bounds-rejected", before, after);
                        _motions.RemoveAt(i);
                        continue;
                    }
                    if (onPlatform)
                    {
                        Vector3 oldPosition = Player.transform.position;
                        Player.Move(worldDelta);
                        Vector3 achieved = Player.transform.position - oldPosition;
                        float required = worldDelta.magnitude;
                        float along = required > 0.000001f ?
                            Vector3.Dot(achieved, worldDelta) / required : 0f;
                        float tolerance = Mathf.Min(0.001f, required * 0.1f);
                        if (required > 0.000001f && along < required - tolerance)
                        {
                            // Undo the sector and restore the exact rider position.
                            // A compensating CharacterController.Move can itself be blocked.
                            bool restored = m.SelectedGeometry
                                ? Builder.RuntimeSetSelectedRaw9Translation(m.SectorId, m.AlongX, before)
                                : Builder.RuntimeSetSectorTranslation(m.SectorId, before);
                            bool enabledBefore = Player.enabled;
                            Player.enabled = false;
                            Player.transform.position = oldPosition;
                            Player.enabled = enabledBefore;
                            Physics.SyncTransforms();
                            Trace(m, restored ? "rider-blocked" : "rollback-failed", after, before);
                            if (!restored) _motions.RemoveAt(i);
                            continue;
                        }
                    }
                    Trace(m, "step", before, after);
                }
                m.Position = next;
                if (m.DosOneWayBounds)
                {
                    // Reproduce only the verified completion truth-table
                    // branches. The DOS mode retains the current command's
                    // mutable flags and timer across automatic reversals.
                    if (originalStep.Reached)
                    {
                        if (m.DosState != null && m.DosState.Complete(
                            m.Repeat, m.RawRevertTicks))
                        {
                            m.DosReverse = m.DosState.Reverse;
                            m.DosBoundWord = m.DosReverse ? m.RawEnd : m.RawStart;
                            RefreshDosGoalForTrace(m);
                            Trace(m, m.DosState.Waiting ? "wait-armed" : "reverse", after, after);
                            TraceDosState(m, m.DosState.Waiting ? "wait-armed" : "reverse");
                        }
                        else
                        {
                            Trace(m, "arrived", after, after);
                            TraceDosState(m, "arrived");
                            _motions.RemoveAt(i);
                        }
                    }
                    continue;
                }
                if (!Mathf.Approximately(next, m.Destination)) continue;
                if (m.Returning)
                {
                    if (!m.Repeat)
                    {
                        Trace(m, "returned", after, after);
                        _motions.RemoveAt(i);
                        continue;
                    }
                    m.Returning = false;
                    m.Destination = m.OutwardDestination;
                    m.ReturnAt = -1f;
                    Trace(m, "repeat", after, after);
                    continue;
                }
                if (m.ReturnDelay <= 0f)
                {
                    if (!m.Repeat)
                    {
                        Trace(m, "arrived", after, after);
                        _motions.RemoveAt(i);
                    }
                    else
                    {
                        m.Returning = true;
                        m.Destination = m.ReturnPosition;
                        Trace(m, "return", after, after);
                    }
                    continue;
                }
                if (m.ReturnAt < 0f) m.ReturnAt = Time.time + m.ReturnDelay;
                if (Time.time < m.ReturnAt) continue;
                if (AvoidPlayerObstruction && Player != null &&
                    Builder.RuntimePlayerOccupiesSector(m.SectorId, Player))
                {
                    m.ReturnAt = Time.time + 0.25f;
                    Trace(m, "return-obstructed", after, after);
                    continue;
                }
                m.Returning = true;
                m.Destination = m.ReturnPosition;
                Trace(m, "return", after, after);
            }
        }
    }
}
