using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Oars
{
    public sealed class EVConnListener : IDisposable
    {
        public Action<IntPtr, IPEndPoint> ConnectionAccepted;
        public EventBase Base { get; set; }
        public IPEndPoint ListenEndPoint { get; private set; }

        IntPtr lev;

        bool disabled;
        Delegate cb;

        public EVConnListener(EventBase eventBase, IPEndPoint endpoint, short backlog)
        {
            Base = eventBase;
            ListenEndPoint = endpoint;

            var options = (short)(ConnectionListenerOptions.CloseOnFree | ConnectionListenerOptions.Reusable);
            var socketAddr = sockaddr_in.FromIPEndPoint(endpoint);
            cb = Delegate.CreateDelegate(typeof(evconnlistener_cb), this, "ConnectionCallback");
            var callbackPointer = Marshal.GetFunctionPointerForDelegate(cb);

            lev = evconnlistener_new_bind(eventBase.Handle, callbackPointer, IntPtr.Zero, 
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
            try
            {
                if (ConnectionAccepted != null)
                    ConnectionAccepted(socket, address.ToIPEndPoint());
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception during connection listener callback.");
                //Extensions.HandleException("EVConnListener callback", e);
            }
        }

        #region interop

        enum ConnectionListenerOptions
        {
            LeaveSocketsBlocking = 1 << 0,
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
