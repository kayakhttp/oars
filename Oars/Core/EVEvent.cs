using System;
using System.Runtime.InteropServices;

namespace Oars
{
    public enum Events : short
    {
        None = 0,
        EV_TIMEOUT = 0x01,
        EV_READ = 0x02,
        EV_WRITE = 0x04,
        EV_SIGNAL = 0x08,
        EV_PERSIST = 0x10//,
        //EV_ET = 0x20,
    }

    public sealed class EVEvent : IDisposable
    {
        public IntPtr Socket { get; private set; }
        public IntPtr Handle { get; private set; }
        public Events Events { get; private set; }

        public Action Activated;
        bool pending;
        Delegate cb;
        IntPtr fp;

        public static EVEvent CreateTimer(EventBase eventBase)
        {
            return CreateTimer(eventBase, false);
        }

        public static EVEvent CreateTimer(EventBase eventBase, bool persist)
        {
            return new EVEvent(eventBase, new IntPtr(-1), persist ? Events.EV_PERSIST : Events.None);
        }

        public EVEvent(EventBase eventBase, IntPtr fd, Events what)
        {
            Socket = fd;
            cb = Delegate.CreateDelegate(typeof(event_callback_fn), this, "EventCallbackInternal");

            fp = Marshal.GetFunctionPointerForDelegate(cb);
            Trace.Write("EVEvent created with fd " + fd.ToInt32().ToString("x") + ", cb " + fp.ToInt32().ToString("x"));
            Handle = event_new(eventBase.Handle, fd, (short)what, fp, IntPtr.Zero);
        }

        public void Dispose()
        {
            Trace.Write("EVEvent disposed with fd " + Socket.ToInt32().ToString("x") + ", cb " + fp.ToInt32().ToString("x"));
            ThrowIfDisposed();

            if (pending)
                Delete();

            event_free(Handle);
            Handle = IntPtr.Zero;
        }

        public void Add(TimeSpan timeout)
        {
            ThrowIfDisposed();
            int result = 0;

            var tv = timeval.FromTimeSpan(timeout);
            unsafe
            {
                result = event_add(Handle, &tv);
            }

            if (result < 0)
                throw new Exception("Failed to add event!");

            pending = true;
        }

        public void Delete()
        {
            ThrowIfDisposed();
            int result = 0;

            result = event_del(Handle);

            if (result < 0)
                throw new Exception("Failed to delete event!");
            pending = false;
        }

        void EventCallbackInternal(IntPtr fd, short what, IntPtr ctx) 
        {
            Events = (Events)what;

            try
            {
                Trace.Write("Event on fd {0} activated with events {1}.", fd.ToInt32(), Events);
                if (Activated != null)
                    Activated();
            }
            catch (Exception e)
            {
                Extensions.HandleException("EVEvent event callback", e);
            }

            Events = Events.None;
        }

        void ThrowIfDisposed()
        {
            if (Handle == IntPtr.Zero)
                throw new ObjectDisposedException("Event");
        }

        private delegate void event_callback_fn(IntPtr fd, short what, IntPtr ctx);

        [DllImport("event_core")]
        static extern IntPtr event_new(IntPtr event_base, IntPtr fd, short what, IntPtr cb, IntPtr ctx);

        [DllImport("event_core")]
        static extern void event_free(IntPtr evnt);

        [DllImport("event_core")]
        static extern unsafe int event_add(IntPtr evnt, timeval* tv);

        [DllImport("event_core")]
        static extern int event_del(IntPtr evnt);

        [DllImport("event_core")]
        static extern int event_priority_set(IntPtr evnt, int priority);
    }
}
