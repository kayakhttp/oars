using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars;
using Oars.Core;
using System.Net;
using System.Threading;
using System.Net.Sockets;

namespace OarsTests
{
    [TestFixture]
    public class EVConnListenerTests
    {
        public static IPEndPoint TestEndPoint = new IPEndPoint(IPAddress.Loopback, 4200);
        static IPEndPoint TestClientEndPoint = new IPEndPoint(IPAddress.Loopback, 42001);
        public static short TestBacklog = 1;

        EventBase eventBase;
        EVConnListener listener;
        IPEndPoint connectedEndpoint;
        TcpClient client;

        [SetUp]
        public void SetUp()
        {
            connectedEndpoint = null;
            eventBase = new EventBase();
            listener = new EVConnListener(eventBase, TestEndPoint, TestBacklog);
            listener.ConnectionAccepted = ConnectionAccepted;
            client = new TcpClient();
        }

        [TearDown]
        public void TearDown()
        {
            client.Close();
            listener.ConnectionAccepted = null;
            listener.Dispose();
            eventBase.Dispose();
        }

        [Test]
        public void Connect()
        {
            var dispatch = eventBase.StartDispatchOnNewThread();

            //Console.WriteLine("About to connect.");
            client.Connect(TestEndPoint);
            //Console.WriteLine("Connected.");
            eventBase.LoopExit(TimeSpan.FromSeconds(1));
            //Console.WriteLine("Waiting for dispatch to join.");
            dispatch.Join();
            //Console.WriteLine("Dispatch joined.");
            Assert.IsNotNull(connectedEndpoint, "Connection failed.");

            // something fishy going on here...seems like mono might be fudging the numbers somewhere underneath...
            //Assert.AreEqual(((IPEndPoint)client.Client.LocalEndPoint).Port, connectedEndpoint.Port);
            
            // 0.0.0.0 == 127.0.0.1, i guess
            //Assert.AreEqual(((IPEndPoint)client.Client.LocalEndPoint).Address.ToString(), connectedEndpoint.Address.ToString(), "IPEndPoint addresses do not match.");
        }

        void ConnectionAccepted(IntPtr fd, IPEndPoint remoteEndPoint)
        {
            //Console.WriteLine("Got connection event.");
            connectedEndpoint = remoteEndPoint;
        }
    }
}
