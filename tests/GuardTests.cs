using System;
using System.Collections.Generic;
using System.IO;
using YAMBF;

internal static class GuardTests
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    // Execute the actual instructions through the branch or WAIT. This checks
    // stack effects and bytecode branch destinations, not only the replacement bytes.
    private static bool Waits(byte[] code, int count)
    {
        int pc = 0;
        var stack = new Stack<int>();
        while (pc < code.Length)
        {
            switch (code[pc++])
            {
                case 0x2C: // NATIVE
                    int signature = code[pc++];
                    pc += 2;
                    if (signature == 1) stack.Push(count);
                    else if (signature == 4)
                    {
                        Check(stack.Pop() == 2000, "WAIT must retain its 2000 ms argument");
                        Check(stack.Count == 0, "WAIT must not leak stack values");
                        return true;
                    }
                    else throw new Exception("Unexpected native signature");
                    break;
                case 0x25: stack.Push(code[pc++]); break; // PUSH_U8
                case 0x43: stack.Push(BitConverter.ToInt16(code, pc)); pc += 2; break;
                case 0x0C: // ILT
                    int right = stack.Pop(), left = stack.Pop();
                    stack.Push(left < right ? 1 : 0); break;
                case 0x0B: // IGE, verified against scrThread.hh
                    right = stack.Pop(); left = stack.Pop();
                    stack.Push(left >= right ? 1 : 0); break;
                case 0x06: stack.Push(stack.Pop() == 0 ? 1 : 0); break; // INOT
                case 0x56: // JZ
                    int relative = BitConverter.ToInt16(code, pc); pc += 2;
                    if (stack.Pop() == 0) pc += relative;
                    break;
                default: throw new Exception("Unexpected opcode at " + (pc - 1));
            }
        }
        Check(pc == code.Length, "Exit branch must land immediately after the loop");
        Check(stack.Count == 0, "Exit branch must leave the stack balanced");
        return false;
    }

    private static byte[] ExtractCode(byte[] resource)
    {
        // RPF resource payload, already decrypted and decompressed; no RSC7 header.
        int size = BitConverter.ToInt32(resource, 0x1C);
        Check(size > 0 && size <= resource.Length, "Invalid script code size");
        int table = (int)(BitConverter.ToUInt64(resource, 0x10) & 0xFFFFFF);
        var code = new byte[size];
        for (int offset = 0, page = 0; offset < size; offset += 0x4000, page++)
        {
            int source = (int)(BitConverter.ToUInt64(resource, table + page * 8) & 0xFFFFFF);
            Buffer.BlockCopy(resource, source, code, offset, Math.Min(0x4000, size - offset));
        }
        return code;
    }

    public static void Main(string[] args)
    {
        byte[] original = {0x2C,1,0,10,0x25,150,0x0C,6,0x56,10,0,0x43,0xD0,7,0x2C,4,0,47,0x55,0xEB,0xFF};
        Check(BlipGuard.Matches(original, 0), "Recognize the installed script's guard");
        var patched = (byte[])original.Clone();
        byte[] comparison = BitConverter.GetBytes(BlipGuard.Patched);
        Buffer.BlockCopy(comparison, 0, patched, 5, comparison.Length);
        Check(BlipGuard.Matches(patched, 0), "Recognize an already patched script");
        for (int count = 0; count <= 2500; count++)
        {
            Check(Waits(original, count) == (count >= 150), "Original behavior at " + count);
            Check(!Waits(patched, count), "Patched behavior at " + count);
        }
        Check(21 + BitConverter.ToInt16(original, 19) == 0, "Backward jump must return to count query");
        Check(!BlipGuard.Matches(original, -1) && !BlipGuard.Matches(original, 1), "Reject invalid bounds");
        Check(!BlipGuard.Matches(new byte[20], 0), "Reject truncated loop");
        var unsupported = (byte[])original.Clone(); unsupported[5] = 149;
        Check(!BlipGuard.Matches(unsupported, 0), "Reject unsupported comparison");
        unsupported = (byte[])original.Clone(); unsupported[9] = 11;
        Check(!BlipGuard.Matches(unsupported, 0), "Reject different branch target");
        var relocated = new byte[original.Length + 20]; Buffer.BlockCopy(original, 0, relocated, 13, original.Length);
        Check(BlipGuard.Matches(relocated, 13), "Recognize relocated loop");
        relocated[15] = 7; relocated[16] = 200; relocated[29] = 4; relocated[30] = 100;
        Check(BlipGuard.Matches(relocated, 13), "Allow changed native table indices");

        if (args.Length != 0)
        {
            byte[] resource = File.ReadAllBytes(args[0]);
            Check(BitConverter.ToUInt32(resource, 0x58) == 0x80CFE974, "Resource must be blip_controller");
            byte[] code = ExtractCode(resource);
            int matches = 0;
            for (int i = 0; i < code.Length; i++) if (BlipGuard.Matches(code, i))
            {
                matches++;
                byte[] loop = new byte[BlipGuard.Length]; Buffer.BlockCopy(code, i, loop, 0, loop.Length);
                Check(Waits(loop, 150), "Installed game's original gate blocks at 150");
                Buffer.BlockCopy(comparison, 0, loop, 5, comparison.Length);
                Check(!Waits(loop, 500), "Installed game's patched gate exits at 500");
                Console.WriteLine("Validated installed guard at bytecode offset 0x" + i.ToString("X"));
            }
            Check(matches == 1, "Expected exactly one supported guard, found " + matches);
        }
        Console.WriteLine("PASS: " + assertions + " assertions");
    }
}
