using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNetworkTester
{
    class Program
    {
        static int PacketLength = 1391;

        static void Main(string[] args)
        {
            int FlowLabel = (Process.GetCurrentProcess().Id ^ (int)DateTime.Now.Ticks) & 0xfffff;
            
            var ipHostEntry = Dns.GetHostEntry("terebi.local");
            if (ipHostEntry.AddressList.Length == 0)
            {
                throw new Exception("Error: Could not resolve terebi.local");
            }
            var TargetIP = ipHostEntry.AddressList[0];
            var RemoteEP = new IPEndPoint(TargetIP, 7);

            var uc = new UdpClient(RemoteEP.AddressFamily);
            uc.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.TypeOfService, (int)46 * 4);
            uc.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IpTimeToLive, (int)6);
            uc.Connect(RemoteEP);
            
            if(RemoteEP.AddressFamily != AddressFamily.InterNetwork && RemoteEP.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new NotSupportedException(String.Format("Address family for {0} is not supported", RemoteEP));
            }

            int pid = Process.GetCurrentProcess().Id;
            ushort SourcePort = (ushort)(pid & 0xffff);
            pid >>= 16;
            SourcePort ^= (ushort)pid;
            if (SourcePort < 1024)
            {
                SourcePort += 1024;
            }
            
            /*
            var udpsock = new Socket(RemoteEP.AddressFamily, SocketType.Raw, ProtocolType.Udp);
            IPEndPoint LocalEP = null;
            if (udpsock.AddressFamily == AddressFamily.InterNetworkV6)
            {
                udpsock.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.HeaderIncluded, 1);
                udpsock.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IpTimeToLive, (int)6);
                LocalEP = new IPEndPoint(IPAddress.IPv6Any, SourcePort);
            }
            if (udpsock.AddressFamily == AddressFamily.InterNetwork)
            {
                udpsock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.HeaderIncluded, 1);
                udpsock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, (int)6);
                LocalEP = new IPEndPoint(IPAddress.Any, SourcePort);
            } */

            /*
            udpsock.Connect(RemoteEP);

            LocalEP = (IPEndPoint)udpsock.LocalEndPoint;
            LocalEP.Port = SourcePort;
            */

            for (int i = 0; true; i++)
            {
                byte[] buffer = new byte[PacketLength-48];
                NoiseGenerator(PacketLength-48).CopyTo(buffer, 0);
                BitConverter.GetBytes(i).Reverse().ToArray().CopyTo(buffer, 0);
                DateTime SendTime = DateTime.Now;
                BitConverter.GetBytes(SendTime.ToBinary()).Reverse().ToArray().CopyTo(buffer, 4);

                /*
                byte[] packet = null;
                if (udpsock.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    packet = BuildV6Headers(buffer, LocalEP, RemoteEP, 6, 46, FlowLabel);
                }
                if (udpsock.AddressFamily == AddressFamily.InterNetwork)
                {
                    packet = BuildV4Headers(buffer, LocalEP, RemoteEP, 6, 46);
                }
                 */

                //udpsock.SendTo(packet, RemoteEP);
                uc.Send(buffer, PacketLength-48);        // 1250 - 40 IPv[46] header - 8 UDP header = 1202 bytes data
                IPEndPoint RemoteSender = null;
                bool DataRead = true;
                try
                {
                    buffer = uc.Receive(ref RemoteSender);
                }
                catch (SocketException e)
                {
                    Console.WriteLine("error: {0}", e.Message);
                    DataRead = false;
                }

                if (DataRead)
                {
                    /*
                    byte[] sequence = new byte[4];
                    byte[] time = new byte[8];
                    Array.ConstrainedCopy(buffer, 0, sequence, 0, 4);
                    Array.ConstrainedCopy(buffer, 4, time, 0, 8);

                    sequence = sequence.Reverse().ToArray();
                    time = time.Reverse().ToArray();

                    int sequenceNum = BitConverter.ToInt32(sequence, 0);
                    long timeNum = BitConverter.ToInt64(time, 0);

                    DateTime timeDT = DateTime.FromBinary(timeNum);

                    TimeSpan timeDelta = DateTime.Now - timeDT;

                    Console.WriteLine("received reply from {0}: seq {1}, {2}ms", RemoteSender.Address, sequenceNum, timeDelta.TotalMilliseconds);
                     */
                }
                //TimeSpan waitTime = new TimeSpan((long)1000) - (DateTime.Now - SendTime);
                TimeSpan waitTime = new TimeSpan(0);

                if (waitTime.Ticks < 0)
                {
                    waitTime = new TimeSpan(0);
                }

                Thread.Sleep(waitTime);
                
            }
            Console.ReadKey();
        }

        static Random _random = new Random();

        static byte[] NoiseGenerator(int length)
        {
            byte[] output = new byte[length];
            _random.NextBytes(output);
            return output;
        }

        static byte[] BuildV6Headers(byte[] payload, IPEndPoint LocalEP, IPEndPoint RemoteEP, int TTL, int DSCP, int FlowLabel)
        {
            byte[] packet = new byte[payload.Length + 48];
            LocalEP.Address.GetAddressBytes().CopyTo(packet, 0);
            RemoteEP.Address.GetAddressBytes().CopyTo(packet, 16);
            int udplen = payload.Length + 8;
            byte[] udplenBytes = BitConverter.GetBytes(udplen);
            if (BitConverter.IsLittleEndian)
            {
                udplenBytes = udplenBytes.Reverse().ToArray();
            }
            udplenBytes.CopyTo(packet, 32);
            byte[] ipv6nexthdr = { 0x0, 0x0, 0x0, 0x11 };
            ipv6nexthdr.CopyTo(packet, 36);

            byte[] sportBytes = BitConverter.GetBytes((ushort)LocalEP.Port);
            if (BitConverter.IsLittleEndian)
            {
                sportBytes = sportBytes.Reverse().ToArray();
            }
            sportBytes.CopyTo(packet, 40);

            byte[] dportBytes = BitConverter.GetBytes((ushort)RemoteEP.Port);
            if (BitConverter.IsLittleEndian)
            {
                dportBytes = dportBytes.Reverse().ToArray();
            }
            dportBytes.CopyTo(packet, 42);

            Array.ConstrainedCopy(udplenBytes, 2, packet, 44, 2);

            byte[] empty = { 0x0, 0x0 };
            empty.CopyTo(packet, 46);

            payload.CopyTo(packet, 48);
            
            int checksum = ComputeChecksum(packet);

            byte[] checksumBytes = BitConverter.GetBytes((short)checksum);
            if (BitConverter.IsLittleEndian)
            {
                checksumBytes = checksumBytes.Reverse().ToArray();
            }
            checksumBytes.CopyTo(packet, 46);

            int v6HeaderRow1 = 6;
            v6HeaderRow1 <<= 8;
            v6HeaderRow1 += DSCP * 4;
            v6HeaderRow1 <<= 20;
            v6HeaderRow1 += FlowLabel & 0xfffff;

            byte[] v6HeaderRow1Bytes = BitConverter.GetBytes(v6HeaderRow1);
            if (BitConverter.IsLittleEndian)
            {
                v6HeaderRow1Bytes = v6HeaderRow1Bytes.Reverse().ToArray();
            }
            v6HeaderRow1Bytes.CopyTo(packet, 0);

            int v6HeaderRow2 = payload.Length + 8;
            v6HeaderRow2 <<= 8;
            v6HeaderRow2 += 17;
            v6HeaderRow2 <<= 8;
            v6HeaderRow2 += TTL & 0xff;

            byte[] v6HeaderRow2Bytes = BitConverter.GetBytes(v6HeaderRow2);
            if (BitConverter.IsLittleEndian)
            {
                v6HeaderRow2Bytes = v6HeaderRow2Bytes.Reverse().ToArray();
            }
            v6HeaderRow2Bytes.CopyTo(packet, 4);

            LocalEP.Address.GetAddressBytes().CopyTo(packet, 8);
            RemoteEP.Address.GetAddressBytes().CopyTo(packet, 24);
            return packet;
        }

        static byte[] BuildV4Headers(byte[] payload, IPEndPoint LocalEP, IPEndPoint RemoteEP, int TTL, int DSCP)
        {
            throw new NotImplementedException();
        }

        static int ComputeChecksum(byte[] packet)
        {
            int Sum = 0;
            for (int i = 0; i < packet.Length; i += 2)
            {
                Sum += (packet[i] << 8);
                Sum += packet[i + 1];
            }
            int SumCarry = Sum >> 16;
            Sum &= 0xffff;
            Sum += SumCarry;
            return ((~Sum) & 0xffff);
        }
    }
}
