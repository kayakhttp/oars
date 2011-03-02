using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Oars
{
    public sealed class EVBuffer : IDisposable
    {
        IntPtr handle;
        bool ownsBuffer;

        public int Length { get { return evbuffer_get_length(handle); } }

        public EVBuffer()
        {
            handle = evbuffer_new();
            ownsBuffer = true;
        }

        public EVBuffer(IntPtr handle)
        {
            this.handle = handle;
        }

        public void Dispose()
        {
            if (ownsBuffer)
                evbuffer_free(handle);
        }

        public bool Add(byte[] data, int offset, int count)
        {
            unsafe {
                fixed (byte *ptr = &data[offset])
                    return evbuffer_add(handle, ptr, count) > 0;
            }
        }

        public int Remove(byte[] data, int offset, int count)
        {
            if (offset + count > data.Length)
                throw new Exception("offset + count > data.Length");

            var c = new IntPtr(count);

            unsafe {
                fixed (byte *ptr = &data[0])
                    return evbuffer_remove(handle, ptr, count);
            }
        }

        public int Remove(EVBuffer buffer, int len)
        {
            return evbuffer_remove_buffer(handle, buffer.handle, len);
        }

        public int Read(IntPtr fd, int count, out bool wouldBlock)
        {
            wouldBlock = false;
            Debug.WriteLine("Attempting to read {0} bytes from socket.", count);
            var result = evbuffer_read(handle, fd, count);

            if (result < 0)
            {
                // reads the C std lib 'errno' value, even on Unix/Mono
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine("Got errno " + error + ".");

                // if we wanted to support windows, we would check for WSAEWOULDBLOCK
                if (error == (int)Errno.EAGAIN)
                    wouldBlock = true;
            }
            else
            {
                Debug.WriteLine("Read {0} bytes from socket.", result);
            }

            return result;
        }

        public int Write(IntPtr fd, int count, out bool wouldBlock)
        {
            wouldBlock = false;
            Debug.WriteLine("Attempting to write {0} bytes to socket.", count);
            var result = evbuffer_write_atmost(handle, fd, count);

            if (result < 0)
            {
                // reads the C std lib 'errno' value, even on Unix/Mono
                var error = Marshal.GetLastWin32Error();
                Debug.WriteLine("Got errno " + error + ".");

                // if we wanted to support windows, we would check for WSAEWOULDBLOCK
                if (error == (int)Errno.EAGAIN)
                    wouldBlock = true;
            }
            else
            {
                Debug.WriteLine("Wrote {0} bytes to socket.", result);
            }

            return result;
        }

        #region interop

        [DllImport("event_core")]
        private static extern IntPtr evbuffer_new();

        [DllImport("event_core")]
        private static extern void evbuffer_free(IntPtr buf);

        [DllImport("event_core")]
        private static unsafe extern int evbuffer_add(IntPtr buf, byte* data, int len);

        [DllImport("event_core")]
        private static unsafe extern int evbuffer_remove(IntPtr buf, byte* data, int len);

        [DllImport("event_core")]
        private static unsafe extern int evbuffer_drain(IntPtr buf, int len);

        [DllImport("event_core")]
        private static extern int evbuffer_remove_buffer(IntPtr src, IntPtr dest, int len);

        [DllImport("event_core")]
        private static extern int evbuffer_add_buffer(IntPtr outbuf, IntPtr inbuf);

        [DllImport("event_core")]
        private static extern int evbuffer_add_file(IntPtr output, IntPtr fd, int offset, int length);

        [DllImport("event_core")]
        private static extern int evbuffer_get_length(IntPtr buf);

        [DllImport("event_core", SetLastError = true)]
        private static extern int evbuffer_read(IntPtr buf, IntPtr sock, int count);

        [DllImport("event_core", SetLastError = true)]
        private static extern int evbuffer_write_atmost(IntPtr buf, IntPtr sock, int count);

        #endregion
    }
}
