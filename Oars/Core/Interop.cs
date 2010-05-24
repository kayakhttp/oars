using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.InteropServices;
using System.Net.Sockets;

namespace Oars.Core
{
    struct timeval
    {
        public int tv_sec;
        public int tv_usec;

        public static timeval FromTimeSpan(TimeSpan ts)
        {
            var sex = (int)(ts.Ticks / 10000000);
            var usex = (int)(ts.Ticks % 10000000) / 10;
            
            return new timeval() { tv_sec = sex, tv_usec = usex };
        }

        public TimeSpan ToTimeSpan()
        {
            return new TimeSpan(tv_usec * 10L + tv_sec * 10000000L);
        }

        public DateTime ToDateTime()
        {
            return new DateTime(1970, 1, 1).AddSeconds(tv_sec).AddMilliseconds(tv_usec / 1000);
        }
    }

    // represents an IPv4 end point

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct sockaddr_in
    {
        public static short StructureLength = 16;

        public short sin_family;
        public ushort sin_port;
        public in_addr sin_addr;
        public fixed byte sin_zero[8];

        public static sockaddr_in FromIPEndPoint(IPEndPoint ep)
        {
            if (ep.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("endpoint.AddressFamily must be AddressFamily.InterNetwork");

            return new sockaddr_in()
            {
                sin_family = IPAddress.HostToNetworkOrder((short)ep.AddressFamily), // seems like host order should work?
                sin_port = (ushort)IPAddress.HostToNetworkOrder((short)ep.Port),
                sin_addr = new in_addr() { s_addr = 0 } // sorry, localhost only!
            };
        }

        public IPEndPoint ToIPEndPoint()
        {
            //var port = IPAddress.NetworkToHostOrder(sin_port);
            var port = sin_port;
            return new IPEndPoint(new IPAddress(IPAddress.NetworkToHostOrder(sin_addr.s_addr)), port);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct in_addr
    {
        public long s_addr;
    }
}
