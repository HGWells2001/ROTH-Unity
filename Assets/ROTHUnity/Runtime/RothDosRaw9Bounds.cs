using System;

namespace ROTHUnity.Runtime
{
    /// <summary>
    /// Read-only bounds planning for one single-shot RAW opcode 9 movement.
    /// Verified DOS callback: forward tests the greatest selected coordinate
    /// against -2 * command[+0xA]; reverse tests the smallest coordinate
    /// against -2 * command[+0xC]. This helper does not model the DOS
    /// animation lifecycle, scheduler or collision response.
    /// </summary>
    public static class RothDosRaw9Bounds
    {
        public static bool TryPlan(short minimum, short maximum,
            short startWord, short endWord, bool reverse,
            int currentOffset, out int destinationOffset, out short target,
            out string reason)
        {
            destinationOffset = currentOffset;
            target = 0;
            reason = null;
            if (minimum > maximum)
            {
                reason = "Original RAW9 coordinate range is inverted";
                return false;
            }

            target = RothDosRaw9Reference.TargetFromCommandWord(
                reverse ? endWord : startWord);
            short current = reverse ? minimum : maximum;
            if (reverse ? target > current : target < current)
            {
                reason = "DOS bound is opposite the selected direction";
                return false;
            }
            // The four vertices translate together. Convert the original DOS
            // absolute coordinate target into an offset for the selected
            // geometry backend, without narrowing intermediate arithmetic.
            long planned = (long)currentOffset + (long)target - current;
            if (planned < int.MinValue || planned > int.MaxValue)
            {
                reason = "Relative RAW9 offset exceeds Int32";
                return false;
            }
            destinationOffset = (int)planned;
            return true;
        }
    }
}
