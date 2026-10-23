using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using GTA;
using GTA.Native;

namespace MissionBlipFix
{
    public sealed class MissionBlipFixScript : Script
    {
        private MethodInfo findPattern;
        private IntPtr ownedPatch;
        private bool disabled;
        private bool online;
        private int nextCheck;
        private int nextDiagnostic;
        private readonly string logPath = Path.Combine("scripts", "MissionBlipFix.log");
        private readonly int controllerHash = unchecked((int)0x80CFE974);

        public MissionBlipFixScript()
        {
            Interval = 500;
            nextCheck = Environment.TickCount;
            nextDiagnostic = nextCheck;
            Tick += OnTick;
            Aborted += OnAborted;
            Log("Starting 1.0.1. Waiting for Story Mode blip_controller.");
        }

        private void OnTick(object sender, EventArgs args)
        {
            if (disabled) return;
            try
            {
                if (Function.Call<bool>(Hash.NETWORK_IS_SESSION_STARTED))
                {
                    Restore();
                    if (!online) Log("Online session detected; suspended.");
                    online = true;
                    return;
                }
                online = false;
                int now = Environment.TickCount;
                if (unchecked(now - nextCheck) < 0) return;
                nextCheck = unchecked(now + 5000);
                if (Function.Call<int>(Hash.GET_NUMBER_OF_THREADS_RUNNING_THE_SCRIPT_WITH_THIS_HASH, controllerHash) == 0)
                    return;

                if (findPattern == null)
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        Type memory = assembly.GetType("SHVDN.NativeMemory", false);
                        if (memory == null) continue;
                        findPattern = memory.GetMethod("FindPatternInScript", BindingFlags.Static | BindingFlags.Public,
                            null, new[] { typeof(string), typeof(int) }, null);
                        if (findPattern != null) break;
                    }
                    if (findPattern == null) throw new NotSupportedException(
                        "SHVDNE FindPatternInScript API is unavailable. Install ScriptHookVDotNet Enhanced 1.1.0.6 or compatible.");
                }
                IntPtr address = FindGuard();
                if (address == IntPtr.Zero) throw new NotSupportedException(
                    "The loaded blip_controller does not contain the supported wait-loop signature. No memory was changed.");
                var bytes = new byte[BlipGuard.Length];
                Marshal.Copy(address, bytes, 0, bytes.Length);
                if (!BlipGuard.Matches(bytes, 0)) throw new NotSupportedException(
                    "The wait-loop comparison is unrecognized. No memory was changed.");

                if (Marshal.ReadInt16(address, 5) == BlipGuard.Original)
                {
                    // Preserve code length, native calls, stack balance, and branch targets.
                    // This changes only the controller's soft count gate, not engine allocation.
                    Marshal.WriteInt16(address, 5, BlipGuard.Patched);
                    if (Marshal.ReadInt16(address, 5) != BlipGuard.Patched)
                        throw new InvalidOperationException("Comparison write verification failed.");
                    ownedPatch = address;
                    Log("Patched blip_controller 150-icon wait gate. " + Status());
                }
                if (unchecked(now - nextDiagnostic) >= 0)
                {
                    Log(Status());
                    nextDiagnostic = unchecked(now + 60000);
                }
            }
            catch (Exception error)
            {
                disabled = true;
                Log("DISABLED: " + error.GetBaseException().Message);
                Restore();
            }
        }

        private IntPtr FindGuard()
        {
            return (IntPtr)findPattern.Invoke(null, new object[] { BlipGuard.Pattern, controllerHash });
        }

        private string Status()
        {
            return "activeBlips=" + Function.Call<int>(Hash.GET_NUMBER_OF_ACTIVE_BLIPS)
                + ", playerModel=" + Game.Player.Character.Model.Hash
                + ", mission=" + Function.Call<bool>(Hash.GET_MISSION_FLAG)
                + ", switching=" + Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS);
        }

        private void OnAborted(object sender, EventArgs args) { Restore(); }

        private void Restore()
        {
            if (ownedPatch == IntPtr.Zero || findPattern == null) return;
            try
            {
                // Resolve again: scripts may have unloaded since the previous tick.
                IntPtr current = FindGuard();
                if (current != ownedPatch) return;
                var bytes = new byte[BlipGuard.Length];
                Marshal.Copy(current, bytes, 0, bytes.Length);
                if (BlipGuard.Matches(bytes, 0) && Marshal.ReadInt16(current, 5) == BlipGuard.Patched)
                {
                    Marshal.WriteInt16(current, 5, BlipGuard.Original);
                    Log("Restored original wait gate.");
                }
            }
            catch (Exception error) { Log("Restore skipped: " + error.GetBaseException().Message); }
            finally { ownedPatch = IntPtr.Zero; }
        }

        private void Log(string message)
        {
            try { File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + Environment.NewLine); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
