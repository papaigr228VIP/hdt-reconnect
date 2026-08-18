using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using Hearthstone_Deck_Tracker.Utility.Logging;
using recroll.native;

namespace recroll
{
    internal sealed class recroll_service
    {
        private const string HearthstoneProcessName = "Hearthstone";

        public recroll_result Disconnect()
        {
            Log.Info("[recroll] Reconnect requested.");

            if (!IsElevated())
            {
                Log.Error("[recroll] HDT is not running as administrator.");
                return recroll_result.Fail("Run Hearthstone Deck Tracker as administrator.");
            }

            Process[] processes = Process.GetProcessesByName(HearthstoneProcessName);

            if (processes.Length == 0)
            {
                Log.Error("[recroll] Hearthstone process was not found.");
                return recroll_result.Fail("Hearthstone process was not found.");
            }

            var pids = new HashSet<uint>();

            foreach (Process process in processes)
            {
                pids.Add((uint)process.Id);
                Log.Info("[recroll] Hearthstone PID: " + process.Id);
            }

            List<recroll_native.MIB_TCPROW_OWNER_MODULE> connections;

            try
            {
                connections = recroll_native.GetAllTcp4Connections();
            }
            catch (Exception ex)
            {
                Log.Error("[recroll] Failed to read TCP table: " + ex);
                return recroll_result.Fail("Could not read Windows TCP table: " + ex.Message);
            }

            Log.Info("[recroll] TCP rows found: " + connections.Count);

            var candidates = connections
                .Where(x =>
                    pids.Contains(x.owningPid)
                    && x.State == recroll_native.tcp_state.ESTABLISHED
                    && x.remoteAddr != 0
                    && x.RemotePort != 0
                )
                .OrderByDescending(x => x.CreateTimestampUtc)
                .ToList();

            Log.Info("[recroll] Hearthstone ESTABLISHED candidates: " + candidates.Count);

            foreach (var candidate in candidates.Take(10))
            {
                Log.Info(string.Format(
                    "[recroll] Candidate PID={0} {1}:{2} -> {3}:{4} Created={5:o}",
                    candidate.owningPid,
                    candidate.LocalAddress,
                    candidate.LocalPort,
                    candidate.RemoteAddress,
                    candidate.RemotePort,
                    candidate.CreateTimestampUtc
                ));
            }

            if (candidates.Count == 0)
            {
                Log.Error("[recroll] No established Hearthstone TCP connection found.");
                return recroll_result.Fail("No established Hearthstone TCP connection was found.");
            }

            var selected = candidates[0];

            Log.Info(string.Format(
                "[recroll] Selected connection: PID={0} {1}:{2} -> {3}:{4}",
                selected.owningPid,
                selected.LocalAddress,
                selected.LocalPort,
                selected.RemoteAddress,
                selected.RemotePort
            ));

            var row = new recroll_native.MIB_TCPROW
            {
                state = selected.state,
                localAddr = selected.localAddr,
                localPort = selected.localPort,
                remoteAddr = selected.remoteAddr,
                remotePort = selected.remotePort
            };

            uint result;

            try
            {
                result = recroll_native.DeleteTcpConnection(ref row);
            }
            catch (Exception ex)
            {
                Log.Error("[recroll] SetTcpEntry threw an exception: " + ex);
                return recroll_result.Fail("SetTcpEntry failed: " + ex.Message);
            }

            if (result != 0)
            {
                string error = GetSetTcpEntryError(result);
                Log.Error("[recroll] SetTcpEntry failed. Code=" + result + " " + error);
                return recroll_result.Fail(error + " (code " + result + ")", result);
            }

            string target = selected.RemoteAddress + ":" + selected.RemotePort;
            Log.Info("[recroll] TCP connection deleted successfully: " + target);
            return recroll_result.Ok("Disconnected " + target);
        }

        private static bool IsElevated()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(identity).IsInRole(
                        WindowsBuiltInRole.Administrator
                    );
                }
            }
            catch
            {
                return false;
            }
        }

        private static string GetSetTcpEntryError(uint code)
        {
            switch (code)
            {
                case 5: return "Access denied. Run HDT as administrator.";
                case 50: return "The requested operation is not supported.";
                case 87: return "Windows rejected the TCP row as an invalid parameter.";
                default: return "Windows SetTcpEntry returned an error.";
            }
        }
    }
}
