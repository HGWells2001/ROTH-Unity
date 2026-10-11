namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Models the verified RAW9 command+2 and animation+5/+6 state branches
    /// from the user's original DOS ROTH.EXE. This class has no Unity
    /// dependencies. It is NOT a complete original animation scheduler.
    /// </summary>
    public sealed class RothDosRaw9CommandState
    {
        public byte CommandState { get; private set; }
        public byte AnimationFlags { get; private set; }
        public ushort WaitTicks { get; private set; }
        public bool Active { get; private set; }
        public bool Reverse { get { return (AnimationFlags & 0x80) != 0; } }
        public bool Waiting { get { return (AnimationFlags & 0x40) != 0; } }

        public RothDosRaw9CommandState(byte commandState)
        {
            CommandState = (byte)(commandState | 0x20);
            AnimationFlags = (byte)((CommandState & 0x02) == 0 ? 0x80 : 0);
            WaitTicks = 0;
            Active = true;
        }

        /// <summary>
        /// Original active-command handler at object1+0x22BB1:
        /// only repeat flag 0x20, zero timeout, and an active command
        /// toggle the direction bit and command-state bit 0x02.
        /// </summary>
        public bool TryActiveRetrigger(bool repeatFlag, ushort timeout)
        {
            if (!Active || (CommandState & 0x20) == 0 ||
                !repeatFlag || timeout != 0) return false;
            AnimationFlags ^= 0x80;
            CommandState ^= 0x02;
            return true;
        }

        /// <summary>
        /// Original wait block at object1+0x23033 uses 16-bit SUB and
        /// signed JG; leave the wraparound remainder visible for traces.
        /// A wait is consumed for this update even if it reaches zero.
        /// </summary>
        public bool ConsumeWait(int elapsedTicks)
        {
            if (!Waiting) return false;
            if (elapsedTicks < 0) return true; // no speculative reverse time
            // x86 SUB word followed by signed JG: the branch is equivalent
            // to comparing the ORIGINAL signed operands, not testing the
            // signed wrapped result. Testing the result loses the overflow
            // flag (OF) and gets waits around 0x8000 wrong.
            ushort elapsedWord = unchecked((ushort)elapsedTicks);
            bool keepWaiting = (short)WaitTicks > unchecked((short)elapsedWord);
            WaitTicks = unchecked((ushort)(WaitTicks - elapsedWord));
            if (!keepWaiting) AnimationFlags &= 0xBF;
            return true;
        }

        /// <summary>
        /// Executes the shared completion branches at object1+0x22FD1.
        /// Returns true only when the animation remains scheduled.
        /// A returned false is the original callback's -1 termination.
        /// </summary>
        public bool Complete(bool repeatFlag, ushort timeout)
        {
            if (!Active) return false;
            if ((AnimationFlags & 0x10) != 0)
            {
                AnimationFlags ^= 0x90;
                return true;
            }

            if (timeout == 0)
            {
                CommandState &= 0xDE;
                if (repeatFlag) CommandState ^= 0x02;
                else CommandState |= 0x08;
                Active = false;
                return false;
            }

            if (repeatFlag && (AnimationFlags & 0x20) != 0)
            {
                CommandState &= 0xDE;
                Active = false;
                return false;
            }

            AnimationFlags ^= 0xE0;
            WaitTicks = timeout;
            return true;
        }
    }
}
