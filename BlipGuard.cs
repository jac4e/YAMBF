using System;

namespace MissionBlipFix
{
    internal static class BlipGuard
    {
        // NATIVE count(0 args, 1 return); PUSH_U8; compare; INOT; JZ +10;
        // PUSH_S16 2000; NATIVE WAIT(1 arg, 0 returns); J -21.
        // Native table indices vary by game update. Jump targets and WAIT must agree.
        internal const string Pattern = "2C 01 ? ? 25 ? ? 06 56 0A 00 43 D0 07 2C 04 ? ? 55 EB FF";
        internal const int Length = 21;
        internal const short Original = 0x0C96; // count < 150
        internal const short Patched = 0x0B00;  // count >= 0

        internal static bool Matches(byte[] code, int offset)
        {
            if (offset < 0 || offset > code.Length - Length) return false;
            string[] parts = Pattern.Split(new[] { ' ' });
            for (int i = 0; i < parts.Length; ++i)
                if (parts[i] != "?" && code[offset + i] != Convert.ToByte(parts[i], 16)) return false;
            short comparison = (short)(code[offset + 5] | code[offset + 6] << 8);
            return comparison == Original || comparison == Patched;
        }
    }
}
