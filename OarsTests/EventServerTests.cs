using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars;
using System.Threading;
using System.Disposables;
using System.Net.Sockets;

namespace OarsTests
{
    [TestFixture]
    public class EventServerTests
    {
        EventServer server;
        bool gotStopping, gotStopped;

        [SetUp]
        public void SetUp()
        {
            server = new EventServer(EVConnListenerTests.TestEndPoint, 2);
        }

        [TearDown]
        public void TearDown()
        {
			server.Dispose();
        }

        [Test]
        public void StartStop()
        {
            var starting = Observable.FromEvent<EventArgs>(server, "Starting").Select(e => "Starting").Take(1);
            var started = Observable.FromEvent<EventArgs>(server, "Started").Select(e => "Started").Take(1);
            var stopping = Observable.FromEvent<EventArgs>(server, "Stopping").Select(e => "Stopping").Take(1);
            var stopped = Observable.FromEvent<EventArgs>(server, "Stopped").Select(e => "Stopped").Take(1);

            var seq = Observable.Concat(starting, started, stopping, stopped);

            var events = new List<string>();

           ManualResetEvent wait = new ManualResetEvent(false);

            var s = seq.Subscribe(e =>
                {
                    //Console.WriteLine("Got event " + e);
                    events.Add(e);
                    if (e == "Started")
                        server.Stop();
                    else if (e == "Stopped")
                        wait.Set();
                });

            server.Start();

            wait.WaitOne();
            wait.Close();
			s.Dispose();

            Assert.AreEqual(4, events.Count);
            Assert.AreEqual("Starting", events[0]);
            Assert.AreEqual("Started", events[1]);
            Assert.AreEqual("Stopping", events[2]);
            Assert.AreEqual("Stopped", events[3]);

            //Console.WriteLine("Start/Stop test complete.");
        }

        [Test]
        public void Connection()
        {	
			var started = Observable.FromEvent<EventArgs>(server, "Started").Select(e => "Started").Take(1);
			var stopping = Observable.FromEvent<EventArgs>(server, "Stopping").Select(e => "Stopping").Take(1);
			var stopped = Observable.FromEvent<EventArgs>(server, "Stopped").Select(e => "Stopped").Take(1);
            var connection = Observable.FromEvent<ConnectionEventArgs>(server, "ConnectionAccepted");

            AutoResetEvent wait = new AutoResetEvent(false);

            var connectionsToMake = 3;
			
			var s0 = started.Concat(stopping).Concat(stopped).Subscribe(e =>
			    {
                    //Console.WriteLine("Got event '" + e + "'.");
                    if (e == "Started")
                        wait.Set();
                    if (e == "Stopping")
                        gotStopping = true;
                    if (e == "Stopped")
                    {
                        gotStopped = true;
                        wait.Set();
                    }
			    });

            int connectionsRecieved = 0;
            int bytesReadFromFinalConnection = -1;

            var s1 = connection.Subscribe(e =>
                {
                    //Console.WriteLine("Got connection event.");
                    if (++connectionsRecieved == connectionsToMake)
                    {
                        //Console.WriteLine("Stopping server.");
                        server.Stop();

                        var stream = e.EventArgs.Connection.GetStream();

                        var buffer = new byte[1];

                        var read = Observable
                            .FromAsyncPattern<byte[], int, int, int>(stream.BeginRead, stream.EndRead)
                            (buffer, 0, buffer.Length)
                            .Subscribe(bytesRead =>
                                {
                                    //Console.WriteLine("Server read " + bytesRead + " bytes. Closing connection.");
                                    bytesReadFromFinalConnection = bytesRead;
                                    e.EventArgs.Connection.Dispose();
                                    //Console.WriteLine("Closed connection.");
                                });
                    }
                    else
                    {
                        //Console.WriteLine("Server closing connection.");
                        e.EventArgs.Connection.Dispose();
                    }
                });
			
			server.Start();
			
            // wait for "Started"
            wait.WaitOne();

            TcpClient client = null;

            foreach (var i in Enumerable.Range(0, connectionsToMake - 1))
            {
                client = new TcpClient();
                // wait until server accepts connection and closes it.
                //Console.WriteLine("Client launching connection.");
                client.Connect(server.ListenEndPoint);
                //Console.WriteLine("Client connected, reading from server.");
                Assert.AreEqual(-1, client.GetStream().ReadByte(), "Didn't get EOF on connection " + i + ".");
            }

            client = new TcpClient();

            // server will begin stopping itself
            //Console.WriteLine("Last client connecting.");
            client.Connect(server.ListenEndPoint);
            //Console.WriteLine("Last client connected.");
            
            // tricky! the read callback (on the server) will execute synchronously with the 
            // connect callback. this means that the "Stopping" event won't happen until
            // after the the read callback returns (since the Stop() call is on the
            // connect callback). Which means that it gotStopping won't be true until after 
            // the next statement (closing the connection from client side). if we added the 
            // ability for event stream to defer its callbacks, we could wait and test for 
            // 'gotStopping' here. but for now we can't wait until after closing the connection.
            // else deadlocks...

            // Close the connection. Server will stop.
            //Console.WriteLine("Last client disconnecting.");
            client.GetStream().Close();
            //Console.WriteLine("Last client disconnected.");

            // wait for "Stopped"
            wait.WaitOne();

            Assert.IsTrue(gotStopping, "Did not get 'Stopping' event.");
            Assert.AreEqual(0, bytesReadFromFinalConnection, "Server did not read 0 bytes from final connection.");
            Assert.IsTrue(gotStopped, "Did not get 'Stopped' event.");

            s0.Dispose();
            s1.Dispose();
            wait.Close();
        }
    }
}
