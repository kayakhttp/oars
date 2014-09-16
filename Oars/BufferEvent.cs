using System;
using System.Runtime.InteropServices;

namespace Oars
{
    public enum BufferEventFlushMode
    {
        Normal = 0,
        Flush = 1,
        Finished = 2
    }

    public sealed class BufferEventEventArgs : EventArgs
    {
        public BufferEventEvents Events { get; private set; }

        internal BufferEventEventArgs(BufferEventEvents events)
        {
            Events = events;
        }
    }

    public sealed class BufferEvent : IDisposable
    {
        bool disposed;
        public EventBase EventBase { get; private set; }
        IntPtr bev;

        Buffer input;
        Buffer output;

        public event EventHandler Read;
        public event EventHandler Write;
        public event EventHandler<BufferEventEventArgs> Event;

        public Buffer Input { get { if (disposed) throw new ObjectDisposedException("Input EVBuffer"); return input ?? (input = new Buffer(bufferevent_get_input(bev))); } }
        public Buffer Output { get { if (disposed) throw new ObjectDisposedException("Ouput EVBuffer"); return output ?? (output = new Buffer(bufferevent_get_output(bev))); } }

        int readLow, readHigh = -1;

        public int ReadLowWatermark
        {
            get { return readLow; }
            set { readLow = value; SetReadWatermark(); }
        }

        public int ReadHighWatermark
        {
            get { return readHigh; }
            set { readHigh = value; SetReadWatermark(); }
        }

        void SetReadWatermark()
        {
            bufferevent_setwatermark(bev, Events.EV_READ, new IntPtr(readLow), new IntPtr(readHigh));
        }

        bufferevent_data_cb readdel;
        bufferevent_data_cb writedel;
        bufferevent_event_cb eventdel;

        public BufferEvent(EventBase eventBase, IntPtr socket, int timeout)
        {
            var options = (int)(BufferEventOptions.CloseOnFree | BufferEventOptions.DeferCallbacks);
            bev = bufferevent_socket_new(eventBase.Handle, socket, options);
            var t = timeval.FromTimeSpan(TimeSpan.FromMilliseconds(timeout));
            bufferevent_set_timeouts(bev, ref t, ref t);
            //Console.WriteLine("bufferevent_socket_new returned " + bev.ToInt32());

            // none of these can throw exceptions.
            readdel = new bufferevent_data_cb(ReadCallbackInternal);
            var readCb = Marshal.GetFunctionPointerForDelegate(readdel);
            writedel = new bufferevent_data_cb(WriteCallbackInternal);
            var writeCb = Marshal.GetFunctionPointerForDelegate(writedel);
            eventdel = new bufferevent_event_cb(EventCallbackInternal);
            var eventCb = Marshal.GetFunctionPointerForDelegate(eventdel);

            bufferevent_setcb(bev, readCb, writeCb, eventCb, IntPtr.Zero);
        }

        void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;
                if (disposing)
                {
                    if (input != null)
                        input.Dispose();
                    if (output != null)
                        output.Dispose();
                    readdel = null;
                    writedel = null;
                    eventdel = null;
                }
                bufferevent_free(bev);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void Enable()
        {
            bufferevent_enable(bev, (short)(Events.EV_READ | Events.EV_WRITE));
        }

        void ReadCallbackInternal(IntPtr bev, IntPtr ctx)
        {
            try
            {
                if (Read != null)
                    Read(this, EventArgs.Empty);
            }
            catch (Exception e)
            {
                Console.WriteLine("A handler of BufferEvent.Read threw an exception.");
                Console.WriteLine(string.Format("[{0}] {1}\n{2}", e.GetType().Name, e.Message, e.StackTrace));
            }
        }

        void WriteCallbackInternal(IntPtr bev, IntPtr ctx)
        {
            try
            {
                if (Write != null)
                    Write(this, EventArgs.Empty);
            }
            catch (Exception e)
            {
                Console.WriteLine("A handler of BufferEvent.Write threw an exception.");
                Console.WriteLine(string.Format("[{0}] {1}\n{2}", e.GetType().Name, e.Message, e.StackTrace));
            }
        }

        void EventCallbackInternal(IntPtr bev, short what, IntPtr ctx)
        {
            try
            {
                if (Event != null)
                    Event(this, new BufferEventEventArgs((BufferEventEvents)what));
            }
            catch (Exception e)
            {
                Console.WriteLine("A handler of BufferEvent.Event threw an exception.");
                Console.WriteLine(string.Format("[{0}] {1}\n{2}", e.GetType().Name, e.Message, e.StackTrace));
            }
        }

        #region interop

        private delegate void bufferevent_data_cb(IntPtr bev, IntPtr ctx);
        private delegate void bufferevent_event_cb(IntPtr bev, short what, IntPtr ctx);

        [Flags]
        enum BufferEventOptions
        {
            CloseOnFree = 1 << 0,
            ThreadSafe = 1 << 1,
            DeferCallbacks = 1 << 2
        }

        [DllImport("event_core")]
        static extern void  bufferevent_set_timeouts(IntPtr bev, ref timeval timeoutread, ref timeval timeoutwrite);

        [DllImport("event_core")]
        static extern IntPtr bufferevent_socket_new(IntPtr event_base, IntPtr socket, int options);

        [DllImport("event_core")]
        static extern void bufferevent_free(IntPtr bev);

        [DllImport("event_core")]
        static extern void bufferevent_setcb(IntPtr bev, IntPtr readcb, IntPtr writecb, IntPtr eventcb, IntPtr ctx);

        [DllImport("event_core")]
        static extern void bufferevent_enable(IntPtr bev, short events);

        [DllImport("event_core")]
        static extern IntPtr bufferevent_get_input(IntPtr bev);

        [DllImport("event_core")]
        static extern IntPtr bufferevent_get_output(IntPtr bev);

        [DllImport("event_core")]
        static extern int bufferevent_flush(IntPtr bev, short iotype, int mode);

        [DllImport("event_core")]
        static extern void bufferevent_setwatermark(IntPtr bev, Events events, IntPtr lowmark, IntPtr highmark);

        #endregion
    }

    [Flags]
    public enum BufferEventEvents
    {
        ReadError = 0x01,
        WriteError = 0x02,
        EOF = 0x10,
        Error = 0x20,
        Timeout = 0x40,
        Connected = 0x80
    }
}
