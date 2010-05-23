using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using Oars.Core;

namespace Oars.Tests
{
    class SocketStreamTest : ITest
    {
        BufferEventSocket socket;
        EventBase bass;
        EVConnListener listener;
        Stream stream;
        byte[] buffer;

        public void Run()
        {
            Console.WriteLine("SocketStreamTest");

            bass = new EventBase();
            listener = new EVConnListener(bass, new IPEndPoint(IPAddress.Any, Program.Port), Program.Backlog);
            listener.ConnectionAccepted += ConnectionAccepted;
            bass.Dispatch();
        }

        void ConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {
            var listener = (EVConnListener)sender;
            Console.WriteLine("accepted a connection from " + e.RemoteEndPoint);
            socket = new BufferEventSocket(bass, e.Socket, e.RemoteEndPoint);
            stream = new BufferEventStream(socket);
            buffer = new byte[1024 * 16];

            socket.BufferEvent.Enable();
            Read();
        }

        void Read()
        {
            Console.Write("reading...");
            stream.BeginRead(buffer, 0, buffer.Length, ReadCallback, null);
        }

        void ReadCallback(IAsyncResult iasr)
        {
            var bytesRead = stream.EndRead(iasr);

            Console.WriteLine("read " + bytesRead + " bytes.");
            if (bytesRead > 0)
            {
                Console.Write("writing...");
                stream.BeginWrite(buffer, 0, bytesRead, WriteCallback, null);
            }
            else
                stream.Close();
        }

        void WriteCallback(IAsyncResult iasr)
        {
            Console.WriteLine("write completed.");
            stream.EndWrite(iasr);
            Read();
        }
    }
}
