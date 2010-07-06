using System;
using System.IO;
using System.Threading;
using Oars.Core;

namespace Oars
{
    /// <summary>
    /// An asynchronous stream implemented in terms of libevent.
    /// TODO: optimize writes, zero-copy file send
    /// </summary>
    public sealed class EventStream : Stream
    {
        EventBase eventBase;
        IntPtr handle;
        FileAccess fileAccess;
        EVEvent readEvent, writeEvent;
        EVBuffer readBuffer, writeBuffer;
        WriteAsyncResult writeResult;
        ReadAsyncResult readResult;
        TimeSpan readTimeout, writeTimeout;
        bool readGotEOF, gotWriteTimeout, gotReadTimeout;

        public EventStream(EventBase eventBase, IntPtr handle, FileAccess fileAccess)
        {
            this.eventBase = eventBase;
            this.handle = handle;
            this.fileAccess = fileAccess;
            readTimeout = writeTimeout = TimeSpan.FromSeconds(2);
        }

        void InitReading()
        {
            Trace.Write("Adding read event.");
            readEvent = new EVEvent(eventBase, handle, Events.EV_READ);
            readEvent.Activated += ReadEventActivated;
            readEvent.Add(readTimeout);

            readBuffer = new EVBuffer();
        }

        void CleanupReading()
        {
            Trace.Write("Releasing read event and buffer.");
            readEvent.Activated -= ReadEventActivated;
            // auto-deletes the event.
            readEvent.Dispose();
            readEvent = null;

            readBuffer.Dispose();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (!CanRead)
                throw new InvalidOperationException("Stream does not support reading.");

            if (readResult != null)
                throw new InvalidOperationException("already reading!");

            if (readEvent == null)
                InitReading();
            
            readResult = new ReadAsyncResult()
            {
                AsyncState = state,
                Callback = callback,
                buffer = buffer,
                offset = offset,
                count = count
            };

            Trace.Write("BeginRead");

            if (readGotEOF || DoRead())
            {
                Trace.Write("Invoking read callback synchronously.");
                readResult.InvokeCallback(true);
            }

            return readResult;
        }

        void ReadEventActivated(object sender, EventArgs e)
        {
            Trace.Write("Read event activated!");

            if (readResult == null)
            {
                Trace.Write("No read pending, discarding event.");
                return;
            }

            if ((readEvent.Events & Events.EV_TIMEOUT) > 0)
            {
                Trace.Write("Got read timeout!");
                gotReadTimeout = true;
                readResult.InvokeCallback(false);
                return;
            }

            if (DoRead())
            {
                Trace.Write("Invoking read callback.");
                readResult.InvokeCallback(false);
            }
        }

        bool DoRead()
        {
            bool wouldBlock;

            //Console.WriteLine("readResult == null?: "+ (readResult == null));
            var bytesToRead = readResult.count - readResult.bytesRead;

            // get bytes off the network.
            Trace.Write("About to read " + bytesToRead + " from socket into buffer.");

            var bytesRead = readBuffer.Read(handle, bytesToRead, out wouldBlock);
            
            if (wouldBlock || bytesRead == -1)
            {
                if (wouldBlock)
                {
                    Trace.Write("Got EWOULDBLOCK/EAGAIN while reading, that's weird.");
                }
                if (bytesRead == -1)
                {
                    Trace.Write("Got error while reading.");
                }
                return false;
            }

            readResult.bytesRead += bytesRead;

            Trace.Write("Read " + bytesRead + " bytes.");
            Trace.Write("Buffer length is " + readBuffer.Length);
            readGotEOF = readResult.bytesRead == 0;

            // copy to managed memory (that's the price we pay for living in our walled garden...)
            var bytesRemoved = readBuffer.Remove(readResult.buffer, readResult.offset, readResult.bytesRead);

            return true;
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (readResult == null)
                throw new InvalidOperationException("Not reading!");

            if (readResult != asyncResult)
                throw new ArgumentException("Bogus IAsyncResult argument.", "asyncResult");

            Trace.Write("EndRead");

            var bytesRead = readResult.bytesRead;
            readResult = null;

            if (gotReadTimeout)
                throw new Exception("Read timed out.");

            return bytesRead;
        }

        void InitWriting()
        {
            writeEvent = new EVEvent(eventBase, handle, Events.EV_WRITE);
            writeEvent.Activated += WriteEventActivated;
            //Console.WriteLine("About to add write event.");
            writeEvent.Add(writeTimeout);
            //Console.WriteLine("Added write event.");

            writeBuffer = new EVBuffer();
        }

        void CleanupWriting()
        {
            Trace.Write("Cleaning write event and buffer.");
            writeEvent.Activated -= WriteEventActivated;
            // auto-deletes the event
            writeEvent.Dispose();
            writeEvent = null;

            writeBuffer.Dispose();
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (!CanWrite)
                throw new InvalidOperationException("Stream does not support reading.");

            if (writeResult != null)
                throw new InvalidOperationException("Already writing.");

            if (writeEvent == null)
                InitWriting();

            writeResult = new WriteAsyncResult()
            {
                AsyncState = state,
                Callback = callback,
                count = count
            };

            Trace.Write("adding " + count + " bytes to output buffer.");
            // TODO this results in an unnecessary copy. look into:
            // evbuffer_add_reference(), GCHandle.Alloc(buffer, GCHandleType.Pinned)
            writeBuffer.Add(buffer, offset, count);
            //Console.WriteLine("output buffer length is " + writeBuffer.Length);

            if (DoWrite())
            {
                Trace.Write("Invoking write callback synchronously.");
                writeResult.InvokeCallback(true);
            }

            return writeResult;
        }

        bool DoWrite()
        {
            bool wouldBlock = false;
            var bytesWritten = writeBuffer.Write(handle, writeBuffer.Length, out wouldBlock);

            if (wouldBlock || bytesWritten == -1)
            {
                if (wouldBlock)
                {
                    Trace.Write("Got EWOULDBLOCK/EAGAIN while writing, that's weird.");
                }
                if (bytesWritten == -1)
                {
                    Trace.Write("Got error while writing.");
                }
                return false;
            }

            writeResult.bytesWritten += bytesWritten;

            Trace.Write("bytes written = {0}, write count = {1}", writeResult.bytesWritten, writeResult.count);
            // i.e., writeBuffer.Length == 0
            return writeResult.bytesWritten == writeResult.count;
        }

        void WriteEventActivated(object sender, EventArgs e)
        {
            Trace.Write("Write event activated!");

            if (writeResult == null)
            {
                Trace.Write("No write pending, discarding event.");
                return;
            }

            if ((writeEvent.Events & Events.EV_TIMEOUT) > 0)
            {
                Trace.Write("Got write timeout!");
                gotWriteTimeout = true;
                writeResult.InvokeCallback(false);
                return;
            }

            if (DoWrite())
            {
                Trace.Write("Invoking write callback.");
                writeResult.InvokeCallback(false);
            }
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (asyncResult != writeResult)
                throw new ArgumentException("asyncResult");

            writeResult = null;

            if (gotWriteTimeout)
            {
                gotWriteTimeout = false;
                throw new Exception("Write timed out.");
            }
        }

        public override void Close()
        {
            base.Close();
            Trace.Write("Closing.");
            if (readEvent != null)
                CleanupReading();

            if (writeEvent != null)
                CleanupWriting();

            disposed = true;
        }

        bool disposed;
        void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException("EventStream");
        }

        public override bool CanRead
        {
            get { return (fileAccess & FileAccess.Read) > 0; }
        }

        public override bool CanWrite
        {
            get { return (fileAccess & FileAccess.Write) > 0; }
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        class AsyncResult : IAsyncResult
        {
            public bool IsCompleted { get; private set; }
            public object AsyncState { get; internal set; }
            public WaitHandle AsyncWaitHandle { get { return null; } }
            public bool CompletedSynchronously { get; private set; }

            internal AsyncCallback Callback { get; set; }

            internal void InvokeCallback(bool synchronous)
            {
                CompletedSynchronously = synchronous;
                IsCompleted = true;
                Callback(this);
            }
        }

        class WriteAsyncResult : AsyncResult
        {
            public int count, bytesWritten;
        }

        class ReadAsyncResult : AsyncResult
        {
            public byte[] buffer;
            public int count;
            public int offset;
            public int bytesRead;
        }
    }
}
