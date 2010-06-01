using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars.Core;
using Oars;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace OarsTests
{
    [TestFixture]
    public class BufferEventStreamTests
    {
        string readFile = "readTest.dat", writeFile = "writeTest.dat";

        string testString;

        EventBase eventBase;
        EventStream stream;
        EVConnListener listener;
        IntPtr connectedSocket;

        TcpClient client;
        bool gotReadCallback, gotWriteCallback, readZeroBytes;
        int serverPosition, numBytesToWrite;
        byte[] intermediateBuffer;
        MemoryStream readDestinationBuffer, writeSourceBuffer;



        [SetUp]
        public void SetUp()
        {
            testString = MakeTestData((int)Math.Round(1024 * 3.14159));
            intermediateBuffer = new byte[1024];
            eventBase = new EventBase();
            listener = new EVConnListener(eventBase, EVConnListenerTests.TestEndPoint, EVConnListenerTests.TestBacklog);
        }

        [TearDown]
        public void TearDown()
        {
            listener.Dispose();
            eventBase.Dispose();
        }

        [Test]
        public void Read()
        {
            readDestinationBuffer = new MemoryStream();

            var dispatch = eventBase.StartDispatchOnNewThread(() =>
            {
                listener.ConnectionAccepted += ReadConnectionAccepted;
            }, () =>
            {
                listener.ConnectionAccepted -= ReadConnectionAccepted;
            });

            Thread.Sleep(0);

            client = new TcpClient();
            client.Connect(listener.ListenEndPoint);
            var clientStream = client.GetStream();
            clientStream.Write(Encoding.UTF8.GetBytes(testString), 0, testString.Length);
            clientStream.Flush();
            client.Close();

            //Console.WriteLine("Waiting for server thread to join.");
            dispatch.Join();
            dispatch = null;
            //Console.WriteLine("Server thread joined.");

            Assert.IsNotNull(connectedSocket, "Never got connection.");
            Assert.IsTrue(gotReadCallback, "Never got read callback.");
            Assert.AreEqual(testString.Length, serverPosition, "Test data length and number of bytes read by stream differ.");
            Assert.IsTrue(readZeroBytes, "Never read zero bytes.");

            var readString = Encoding.UTF8.GetString(readDestinationBuffer.ToArray());
            Assert.AreEqual(testString, readString, "Test data and data read by stream differ.");

            //Console.WriteLine("Read test done.");
        }

        #region Read test helpers

        void ReadConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {
            //Console.WriteLine("Read connection accepted");
            connectedSocket = e.Socket;

            stream = new EventStream(eventBase, connectedSocket, FileAccess.Read);
            BeginRead();
        }

        void BeginRead()
        {
            //Console.WriteLine("Begin read.");
            // TODO use smaller buffer size to induce multiple reads.
            stream.BeginRead(intermediateBuffer, 0, intermediateBuffer.Length, ReadCallback, null);
        }

        void ReadCallback(IAsyncResult iasr)
        {
            //Console.WriteLine("Got read callback.");
            gotReadCallback = true;
            var n = stream.EndRead(iasr);
            serverPosition += n;
            //Console.WriteLine("Stream read " + n + " bytes.");
            readDestinationBuffer.Write(intermediateBuffer, 0, n);

            readZeroBytes = n == 0;

            if (readZeroBytes || serverPosition > testString.Length)
            {
                //Console.WriteLine("Closing socket.");
                stream.Dispose();
                connectedSocket.Close();
                //Console.WriteLine("Exiting loop.");
                eventBase.LoopExit();
            }
            else
                BeginRead();
        }

        #endregion


        [Test]
        public void Write()
        {
            //Console.WriteLine("Write test began.");
            writeSourceBuffer = new MemoryStream(Encoding.UTF8.GetBytes(testString));
            readDestinationBuffer = new MemoryStream();

            var dispatch = eventBase.StartDispatchOnNewThread(() =>
            {
                listener.ConnectionAccepted += WriteConnectionAccepted;
            }, () =>
            {
                listener.ConnectionAccepted -= WriteConnectionAccepted;
            });

            Thread.Sleep(0);

            client = new TcpClient();
            client.Connect(listener.ListenEndPoint);
            //Console.WriteLine("Client connected.");
            ReadFromClient();
            client.Close();

            //Console.WriteLine("Waiting for server thread to join.");
            dispatch.Join();
            //Console.WriteLine("Server thread joined.");

            Assert.IsNotNull(connectedSocket, "Never got connection.");
            Assert.IsTrue(gotWriteCallback, "Never got write callback.");

            var readString = Encoding.UTF8.GetString(readDestinationBuffer.ToArray());

            Assert.AreEqual(testString, readString, "Length of test string and string received differ.");
            Assert.AreEqual(testString, readString, "Contents of test string and string received differ.");
        }

        void WriteConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {
            //Console.WriteLine("Got connection event.");
            connectedSocket = e.Socket;

            stream = new EventStream(eventBase, connectedSocket, FileAccess.Write);
            BeginWrite();
        }

        void BeginWrite()
        {
            numBytesToWrite = writeSourceBuffer.Read(intermediateBuffer, 0, intermediateBuffer.Length);
            //Console.WriteLine("Read " + numBytesToWrite + " from write source buffer.");
            if (numBytesToWrite > 0)
                stream.BeginWrite(intermediateBuffer, 0, numBytesToWrite, WriteCallback, null);
            else
            {
                //Console.WriteLine("closing socket.");
                stream.Dispose();
                connectedSocket.Close();
                eventBase.LoopExit();
            }
        }

        void WriteCallback(IAsyncResult iasr)
        {
            //Console.WriteLine("Got write callback.");
            gotWriteCallback = true;
            stream.EndWrite(iasr);
            serverPosition += numBytesToWrite;
            BeginWrite();
        }

        void ReadFromClient()
        {
            var stream = client.GetStream();

            byte[] buffer = new byte[1024];

            while (true)
            {
                //Console.WriteLine("About to read from server.");
                var n = stream.Read(buffer, 0, buffer.Length);
                //Console.WriteLine("Read " + n + " bytes from server.");

                if (n == 0)
                    break;

                readDestinationBuffer.Write(buffer, 0, n);
            }
        }

        #region test data generator

        string MakeTestData(int howMuch)
        {
            return new string(DataGenerator().Take(howMuch).ToArray());
        }

        static char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
        static Random random = new Random();

        IEnumerable<char> DataGenerator()
        {
            while (true)
                yield return chars.ElementAt(random.Next(chars.Length));
        }

        #endregion
    }
}
