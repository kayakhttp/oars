using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Oars.Core
{
    public enum Events : short
    {
        None = 0,
        EV_TIMEOUT = 0x01,
        EV_READ = 0x02,
        EV_WRITE = 0x04,
        EV_SIGNAL = 0x08//,
        //EV_PERSIST = 0x10,
        //EV_ET = 0x20,
    }

    public class Event : IDisposable
    {
        public IntPtr Handle { get; private set; }

        public event EventHandler Activated;
        bool pending;

        public Event(EventBase eventBase, IntPtr fd, Events what)
        {
            Handle = event_new(eventBase.Handle, fd, (short)what, Marshal.GetFunctionPointerForDelegate(new event_callback_fn(EventCallbackInternal)), IntPtr.Zero);
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            if (!pending)
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
            if (Activated != null)
                Activated(this, EventArgs.Empty);
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

    public class EventTimer : Event
    {
        public EventTimer(EventBase eventBase) : base(eventBase, new IntPtr(-1), Events.None) { }
    }
}
