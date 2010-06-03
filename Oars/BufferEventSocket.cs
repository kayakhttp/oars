using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using Oars.Core;

namespace Oars
{
    public class BufferEventStream : Stream
    {
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

        class ReadAsyncResult : AsyncResult
        {
            public byte[] buffer;
            public int count;
            public int offset;
            public int bytesRead;
        }

        BufferEvent bufferEvent;

        ReadAsyncResult readResult;
        AsyncResult writeResult;

        bool gotEndOfFile, ownsBufferEvent;

        public BufferEventStream(BufferEvent bufferEvent) : this(bufferEvent, true) { }
        public BufferEventStream(BufferEvent bufferEvent, bool ownsBufferEvent)
        {
            this.bufferEvent = bufferEvent;
            this.ownsBufferEvent = ownsBufferEvent;
            bufferEvent.Event += bufferEvent_Event;
            bufferEvent.Read += bufferEvent_Read;
            bufferEvent.Write += bufferEvent_Write;
        }

        void bufferEvent_Event(object sender, BufferEventEventArgs e)
        {
            var what = e.Events;
            Console.WriteLine("got event " + what.ToString());
            if ((what & BufferEventEvents.EOF) > 0)
                InvokeReadCallbackIfNecessary(false);
            //if ((what & BufferEventEvents.Error) > 0)  
            // TODO throw on next access?

        }

        void InvokeReadCallbackIfNecessary(bool synchronous)
        {
            if (gotEndOfFile)
                readResult.bytesRead = 0;
            else
            {
                // TODO if -1, there was an error!
                var bytesToRead = Math.Min(readResult.count, bufferEvent.Input.Length);
                readResult.bytesRead = bufferEvent.Input.Remove(readResult.buffer, readResult.offset, bytesToRead);

                if (readResult.bytesRead == 0 && !gotEndOfFile)
                    return; // wait for next callback
            }
            
            readResult.InvokeCallback(synchronous);
        }

        void bufferEvent_Read(object sender, EventArgs e)
        {
            Console.WriteLine("bufferEvent read callback");
            if (readResult != null)
                InvokeReadCallbackIfNecessary(false);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (readResult != null)
                throw new InvalidOperationException("Already reading!");

            readResult = new ReadAsyncResult()
            {
                AsyncState = state,
                Callback = callback,
                buffer = buffer,
                offset = offset,
                count = count
            };

            // TODO defer so stack doesn't grow without bounds
            InvokeReadCallbackIfNecessary(true);

            return readResult;
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (asyncResult != readResult)
                throw new ArgumentException("asyncResult");

            var bytesRead = readResult.bytesRead;
            readResult = null;
            return bytesRead;
        }

        void bufferEvent_Write(object sender, EventArgs e)
        {
            Console.WriteLine("bufferEvent write callback");
            if (writeResult == null)
            {
                Console.WriteLine("spurious write!?");
                return;
            }

            writeResult.InvokeCallback(false);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (writeResult != null)
                throw new InvalidOperationException("Already writing!");

            writeResult = new AsyncResult()
            {
                Callback = callback,
                AsyncState = state
            };

            bufferEvent.Output.Add(buffer, offset, count);

            return writeResult;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (asyncResult != writeResult)
                throw new ArgumentException("asyncResult");

            writeResult = null;
        }

        public override void Close()
        {
            if (ownsBufferEvent)
                bufferEvent.Dispose();
        }

        public void Dispose()
        {
            Close();
        }

        public override void Flush() { throw new NotSupportedException("BufferEvent does not need to be flushed. When a(n) async/sync write call completes/returns, it's done (the underlying bev notifies us after its buffer has been emptied)."); }
        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }


}
