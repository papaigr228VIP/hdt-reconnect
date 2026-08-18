using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;

namespace recroll.native
{
    internal static class recroll_native
    {
        private const int AF_INET = 2;
        private const uint NO_ERROR = 0;

        internal enum tcp_state : uint
        {
            CLOSED = 1,
            LISTEN = 2,
            SYN_SENT = 3,
            SYN_RECEIVED = 4,
            ESTABLISHED = 5,
            FIN_WAIT_1 = 6,
            FIN_WAIT_2 = 7,
            CLOSE_WAIT = 8,
            CLOSING = 9,
            LAST_ACK = 10,
            TIME_WAIT = 11,
            DELETE_TCB = 12
        }

        private enum tcp_table_class
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MIB_TCPROW
        {
            public uint state;
            public uint localAddr;
            public uint localPort;
            public uint remoteAddr;
            public uint remotePort;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MIB_TCPROW_OWNER_MODULE
        {
            public uint state;
            public uint localAddr;
            public uint localPort;
            public uint remoteAddr;
            public uint remotePort;
            public uint owningPid;
            public long liCreateTimestamp;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public ulong[] OwningModuleInfo;

            public tcp_state State => (tcp_state)state;
            public ushort RemotePort => NetworkPortToHost(remotePort);
            public ushort LocalPort => NetworkPortToHost(localPort);
            public IPAddress RemoteAddress => new IPAddress(remoteAddr);
            public IPAddress LocalAddress => new IPAddress(localAddr);

            public DateTime CreateTimestampUtc
            {
                get
                {
                    if (liCreateTimestamp <= 0)
                        return DateTime.MinValue;
                    try { return DateTime.FromFileTimeUtc(liCreateTimestamp); }
                    catch { return DateTime.MinValue; }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPTABLE_OWNER_MODULE
        {
            public uint dwNumEntries;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 1)]
            public MIB_TCPROW_OWNER_MODULE[] table;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwOutBufLen,
            bool sort,
            int ipVersion,
            tcp_table_class tableClass,
            uint reserved
        );

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint SetTcpEntry(ref MIB_TCPROW tcpRow);

        internal static List<MIB_TCPROW_OWNER_MODULE> GetAllTcp4Connections()
        {
            int bufferSize = 0;

            GetExtendedTcpTable(
                IntPtr.Zero, ref bufferSize, false, AF_INET,
                tcp_table_class.TCP_TABLE_OWNER_MODULE_ALL, 0
            );

            if (bufferSize <= 0)
                throw new InvalidOperationException("GetExtendedTcpTable returned an invalid buffer size.");

            IntPtr buffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                uint result = GetExtendedTcpTable(
                    buffer, ref bufferSize, false, AF_INET,
                    tcp_table_class.TCP_TABLE_OWNER_MODULE_ALL, 0
                );

                if (result != NO_ERROR)
                    throw new Win32Exception((int)result);

                int count = Marshal.ReadInt32(buffer);
                int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_MODULE));
                int tableOffset = Marshal.OffsetOf(
                    typeof(MIB_TCPTABLE_OWNER_MODULE), "table"
                ).ToInt32();

                IntPtr rowPointer = IntPtr.Add(buffer, tableOffset);
                var connections = new List<MIB_TCPROW_OWNER_MODULE>(count);

                for (int i = 0; i < count; i++)
                {
                    var row = (MIB_TCPROW_OWNER_MODULE)Marshal.PtrToStructure(
                        rowPointer, typeof(MIB_TCPROW_OWNER_MODULE)
                    );
                    connections.Add(row);
                    rowPointer = IntPtr.Add(rowPointer, rowSize);
                }

                return connections;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        internal static uint DeleteTcpConnection(ref MIB_TCPROW row)
        {
            row.state = (uint)tcp_state.DELETE_TCB;
            return SetTcpEntry(ref row);
        }

        internal static ushort NetworkPortToHost(uint networkPort)
        {
            byte[] bytes = BitConverter.GetBytes(networkPort);
            return (ushort)((bytes[0] << 8) | bytes[1]);
        }
    }
}
