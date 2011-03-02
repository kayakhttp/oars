using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars;
using Oars.Core;
using System.Net.Sockets;
using System.IO;
using System.Net;

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
        bool writeTest;

        [SetUp]
        public void SetUp()
        {
            eventBase = new EventBase();
            listener = new EVConnListener(eventBase, EVConnListenerTests.TestEndPoint, EVConnListenerTests.TestBacklog);
            listener.ConnectionAccepted = ConnectionAccepted;
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

        [Test]
        public void TestWrite()
        {
            writeTest = true;

            var dispatch = eventBase.StartDispatchOnNewThread();
 
            client.Connect(EVConnListenerTests.TestEndPoint);
            //Console.WriteLine("Connected.");

            var stream = client.GetStream();
            
            byte[] recievedData = new byte[testData.Length];
            //Console.WriteLine("About to read.");
            stream.Read(recievedData, 0, recievedData.Length);
            //Console.Write("Read some data.");

            // if this works, then the network hardware would have to say that it was written
            // before the read completes.
            dispatch.Join();
            //Console.Write("Dispatch joined.");

            var recievedString = Encoding.UTF8.GetString(recievedData);

            Assert.AreEqual(testString, recievedString);
        }

        void ConnectionAccepted(IntPtr socket, IPEndPoint ep)
        {
            bufferEvent = new BufferEvent(eventBase, socket);
            bufferEvent.Read += Read;
            bufferEvent.Write += Write;
            bufferEvent.Event += Event;
            bufferEvent.Enable();

            if (writeTest)
            {
                EVBuffer output = bufferEvent.Output;
                output.Add(testData, 0, testData.Length);
                //Console.WriteLine("added data");
            }
        }

        void Write(object sender, EventArgs e)
        {
            eventBase.LoopExit();
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
