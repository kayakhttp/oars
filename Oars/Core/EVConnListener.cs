using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;

namespace Oars.Core
{
    public sealed class ConnectionAcceptedEventArgs : EventArgs
    {
        public IntPtr Socket { get; private set; }
        public IPEndPoint RemoteEndPoint { get; private set; }

        internal ConnectionAcceptedEventArgs(IntPtr socket, IPEndPoint remoteEndPoint)
        {
            Socket = socket;
            RemoteEndPoint = remoteEndPoint;
        }
    }

    public sealed class EVConnListener : IDisposable
    {
        public event EventHandler<ConnectionAcceptedEventArgs> ConnectionAccepted;
        public EventBase Base { get; set; }
        public IPEndPoint ListenEndPoint { get; private set; }

        IntPtr lev;

        bool disabled;

        public EVConnListener(EventBase eventBase, IPEndPoint endpoint, short backlog)
        {
            Base = eventBase;
            ListenEndPoint = endpoint;

            var options = (short)(ConnectionListenerOptions.CloseOnFree | ConnectionListenerOptions.Reusable);
            var socketAddr = sockaddr_in.FromIPEndPoint(endpoint);
            var cb = Marshal.GetFunctionPointerForDelegate(new evconnlistener_cb(ConnectionCallback));

            lev = evconnlistener_new_bind(eventBase.Handle, cb, IntPtr.Zero, 
                options, backlog, ref socketAddr, sockaddr_in.StructureLength);

            if (lev == IntPtr.Zero)
                throw new Exception("could not create ConnectionListener");
        }

        public void Dispose()
        {
            evconnlistener_free(lev);
        }

        public void Enable()
        {
            if (!disabled) throw new InvalidOperationException("not disabled!");
            evconnlistener_enable(lev);
        }

        public void Disable()
        {
            if (disabled) throw new InvalidOperationException("already disabled!");

            evconnlistener_disable(lev);
            disabled = true;
        }
        void ConnectionCallback(IntPtr listener, IntPtr socket, sockaddr_in address, int socklen, IntPtr ctx)
        {
            if (ConnectionAccepted != null)
                ConnectionAccepted(this, new ConnectionAcceptedEventArgs(socket, address.ToIPEndPoint()));
        }

        #region interop

        enum ConnectionListenerOptions
        {
            //LeaveSocketsBlocking = 1 << 0,
            CloseOnFree = 1 << 1,
            //CloseOnExec = 1 << 2,
            Reusable = 1 << 3
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void evconnlistener_cb(IntPtr listener, IntPtr socket, sockaddr_in address, int socklen, IntPtr ctx);

        [DllImport("event_core")]
        private unsafe static extern IntPtr evconnlistener_new_bind(IntPtr event_base, IntPtr cb, IntPtr ctx, short flags, short backlog, ref sockaddr_in sa, short socklen);

        [DllImport("event_core")]
        private static extern void evconnlistener_free(IntPtr lev);

        [DllImport("event_core")]
        private static extern IntPtr evconnlistener_get_base(IntPtr lev);

        [DllImport("event_core")]
        private static extern int evconnlistener_enable(IntPtr lev);

        [DllImport("event_core")]
        private static extern int evconnlistener_disable(IntPtr lev);

        #endregion
    }
}
