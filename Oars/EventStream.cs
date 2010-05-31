using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net.Sockets;
using Oars.Core;
using System.Threading;
using System.Runtime.InteropServices;

namespace Oars
{
    public class EventStream : Stream
    {
        EventBase eventBase;
        IntPtr handle;
        FileAccess fileAccess;
        Event readEvent, writeEvent;
        EVBuffer readBuffer, writeBuffer;
        WriteAsyncResult writeResult;
        ReadAsyncResult readResult;
        TimeSpan readTimeout, writeTimeout;
        bool readGotEOF;

        public EventStream(EventBase eventBase, IntPtr handle, FileAccess fileAccess)
        {
            this.eventBase = eventBase;
            this.handle = handle;
            this.fileAccess = fileAccess;
            readTimeout = writeTimeout = TimeSpan.FromSeconds(2);
        }

        void InitReading()
        {
            readEvent = new Event(eventBase, handle, Events.EV_READ);
            readEvent.Activated += ReadEventActivated;

            readBuffer = new EVBuffer();
        }

        void CleanupReading()
        {
            readEvent.Activated -= ReadEventActivated;
            // auto-deletes the event.
            readEvent.Dispose();

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

            if (readGotEOF)
                readResult.InvokeCallback(true);

            //Console.WriteLine("Adding read event.");
            readEvent.Add(readTimeout);

            return readResult;
        }

        void ReadEventActivated(object sender, EventArgs e)
        {
            //Console.WriteLine("Read event activated!");
            //Console.WriteLine("Events were: " + readEvent.Events);
            if ((readEvent.Events & Events.EV_TIMEOUT) > 0)
            {
                Console.WriteLine("Got timeout!");
                return; // TODO handle timeout
            }

            bool wouldBlock;

            var bytesToRead = readResult.count - readResult.bytesRead;
            
            // get bytes off the network.
            var bytesRead = readBuffer.Read(handle, bytesToRead, out wouldBlock);
            readResult.bytesRead += bytesRead;

            //Console.WriteLine("read " + bytesRead + " bytes");
            //Console.WriteLine("buffer length is " + readBuffer.Length);

            //Console.WriteLine("would block? " + wouldBlock);

            if (wouldBlock)
            {
                // TODO shouldn't happen/handle timeout?
                Console.WriteLine("Got EWOULDBLOCK/EAGAIN while reading, that's weird.");
                return;
            }

            readGotEOF = readResult.bytesRead == 0;
            
            // copy to managed memory (that's the price we pay for living in our walled garden...)
            readBuffer.Remove(readResult.buffer, readResult.offset, readResult.bytesRead);

            // no more notifications until user expresses interest (BeginRead())
            readEvent.Delete();

            //Console.WriteLine("Invoking read callback.");
            readResult.InvokeCallback(false);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (readResult == null)
                throw new InvalidOperationException("Not reading!");

            if (readResult != asyncResult)
                throw new ArgumentException("Bogus IAsyncResult argument.", "asyncResult");

            var bytesRead = readResult.bytesRead;
            readResult = null;
            return bytesRead;
        }

        void InitWriting()
        {
            writeEvent = new Event(eventBase, handle, Events.EV_WRITE);
            writeEvent.Activated += WriteEventActivated;

            writeBuffer = new EVBuffer();
        }

        void CleanupWriting()
        {
            writeEvent.Activated -= WriteEventActivated;
            // auto-deletes the event
            writeEvent.Dispose();

            writeBuffer.Dispose();
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (!CanWrite)
                throw new InvalidOperationException("Stream does not support reading.");

            if (writeResult != null)
                throw new InvalidOperationException("Already reading.");

            if (writeEvent == null)
                InitWriting();

            writeResult = new WriteAsyncResult()
            {
                AsyncState = state,
                Callback = callback,
                count = count
            };

            writeEvent.Add(writeTimeout);
            writeBuffer.Add(buffer, offset, count);

            return writeResult;
        }

        void WriteEventActivated(object sender, EventArgs e)
        {
            //Console.WriteLine("Write event activated!");
            if ((writeEvent.Events & Events.EV_TIMEOUT) > 0)
            {
                Console.WriteLine("Got timeout!");
                return; // TODO handle timeout
            }

            bool wouldBlock = false;
            var bytesWritten = writeBuffer.Write(handle, writeBuffer.Length, out wouldBlock);

            if (wouldBlock)
            {
                // TODO shouldn't happen/handle timeout?
                Console.WriteLine("Got EWOULDBLOCK/EAGAIN while writing, that's weird.");
                return;
            }

            writeResult.bytesWritten += bytesWritten;

            //Console.WriteLine("bytesWritten = " + writeResult.bytesWritten);
            //Console.WriteLine("count = " + writeResult.count);
            if (writeResult.bytesWritten == writeResult.count)
                writeResult.InvokeCallback(false);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (asyncResult != writeResult)
                throw new ArgumentException("asyncResult");

            writeResult = null;
        }

        public override void Close()
        {
            if (readEvent != null)
                CleanupReading();

            if (writeEvent != null)
                CleanupWriting();
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
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
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
