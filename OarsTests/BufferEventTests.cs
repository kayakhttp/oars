using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars;
using Oars.Core;
using System.Net.Sockets;
using System.IO;

namespace OarsTests
{
    [TestFixture]
    public class BufferEventTests
    {
        static string testString = "OMG THIS IS TEST DATA WTF";
        static byte[] testData = Encoding.UTF8.GetBytes(testString);

        EventBase eventBase;
        EVConnListener listener;
        BufferEvent bufferEvent;
        TcpClient client;
        MemoryStream readData;
        bool error;

        [SetUp]
        public void SetUp()
        {
            eventBase = new EventBase();
            listener = new EVConnListener(eventBase, EVConnListenerTests.TestEndPoint, EVConnListenerTests.TestBacklog);
            listener.ConnectionAccepted += ConnectionAccepted;
            client = new TcpClient();
            readData = new MemoryStream();
        }

        [TearDown]
        public void TearDown()
        {
            client.Close();
            bufferEvent.Dispose();
            listener.ConnectionAccepted -= ConnectionAccepted;
            listener.Dispose();
            eventBase.Dispose();
        }

        [Test]
        public void TestRead()
        {
            var dispatch = eventBase.StartDispatchOnNewThread();

            client.Connect(EVConnListenerTests.TestEndPoint);
            var stream = client.GetStream();

            stream.Write(testData, 0, testData.Length);
            stream.Flush();

            stream.Close();

            dispatch.Join();

            Assert.IsFalse(error, "BufferEvent encountered an error");

            var readString = Encoding.UTF8.GetString(readData.ToArray());
            Assert.AreEqual(testString, readString);
        }

        void ConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {
            bufferEvent = new BufferEvent(eventBase, e.Socket);
            bufferEvent.Read += Read;
            bufferEvent.Write += Write;
            bufferEvent.Event += Event;
            bufferEvent.Enable();
        }

        void Write(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }

        void Read(object sender, EventArgs e)
        {
            EVBuffer input = bufferEvent.Input;

            var buffer = new byte[16];
            int bytesRead = 0;

            do
            {
                bytesRead = input.Remove(buffer, 0, buffer.Length);
                readData.Write(buffer, 0, bytesRead);
            }
            while (bytesRead > 0);
        }

        void Event(object sender, BufferEventEventArgs e)
        {
            if ((e.Events & BufferEventEvents.EOF) > 0)
                eventBase.LoopExit();
            if ((e.Events & BufferEventEvents.Error) > 0)
                error = true;
        }
    }
}
