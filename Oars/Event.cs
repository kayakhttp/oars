using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Oars
{
    [Flags]
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

    public sealed class Event : IDisposable
    {
        IntPtr fd;
        IntPtr fp;
        public IntPtr Handle { get; private set; }
        public Events Events { get; private set; }

        public event EventHandler Activated;
        bool pending;
        Delegate cb;

        public static Event CreateTimer(EventBase eventBase)
        {
            return CreateTimer(eventBase, false);
        }

        public static Event CreateTimer(EventBase eventBase, bool persist)
        {
            return new Event(eventBase, new IntPtr(-1), persist ? Events.EV_PERSIST : Events.None);
        }

        public Event(EventBase eventBase, IntPtr fd, Events what)
        {
            this.fd = fd;
            cb = Delegate.CreateDelegate(typeof(event_callback_fn), this, "EventCallbackInternal");

            fp = Marshal.GetFunctionPointerForDelegate(cb);
            Debug.WriteLine("EVEvent created with fd " + fd.ToInt32().ToString("x") + ", cb " + fp.ToInt32().ToString("x"));
            Handle = event_new(eventBase.Handle, fd, (short)what, fp, IntPtr.Zero);
        }

        public void Dispose()
        {
            Debug.WriteLine("EVEvent disposed with fd " + fd.ToInt32().ToString("x") + ", cb " + fp.ToInt32().ToString("x"));
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
                Debug.WriteLine("Event on fd {0} activated with events {1}.", fd.ToInt32(), Events);
                if (Activated != null)
                    Activated(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                Debug.WriteLine("Exception during event callback.");
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
