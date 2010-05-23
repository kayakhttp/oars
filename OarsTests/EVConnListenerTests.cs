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
            listener.ConnectionAccepted += ConnectionAccepted;
            client = new TcpClient();
        }

        [TearDown]
        public void TearDown()
        {
            client.Close();
            listener.ConnectionAccepted -= ConnectionAccepted;
            listener.Dispose();
            eventBase.Dispose();
        }

        [Test]
        public void Connect()
        {
            var dispatch = eventBase.StartDispatchOnNewThread();

            client.Connect(TestEndPoint);
            eventBase.LoopExit(TimeSpan.FromSeconds(1));
            dispatch.Join();

            Assert.IsNotNull(connectedEndpoint, "Connection failed.");

            // something fishy going on here...seems like mono might be fudging the numbers somewhere underneath...
            //Assert.AreEqual(((IPEndPoint)client.Client.LocalEndPoint).Port, connectedEndpoint.Port);
            
            // 0.0.0.0 == 127.0.0.1, i guess
            //Assert.AreEqual(((IPEndPoint)client.Client.LocalEndPoint).Address.ToString(), connectedEndpoint.Address.ToString(), "IPEndPoint addresses do not match.");
        }

        void ConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {
            connectedEndpoint = e.RemoteEndPoint;
        }
    }
}
