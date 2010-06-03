using System;
using System.IO;
using System.Net;
using System.Threading;
using Oars.Core;

namespace Oars
{
    /// <summary>
    /// Simple eventful TCP server. It's your responsibility to close the connections it throws off!
    /// </summary>
    public class EventServer : IDisposable
    {
        public IPEndPoint ListenEndPoint { get; private set; }

		public event EventHandler Starting, Started, Stopping, Stopped;
        public event EventHandler<ConnectionEventArgs> ConnectionAccepted;

        Thread dispatch;

        // only access from dispatch thread!
        EventBase eventBase;
        EVEvent exitTimer;
        EVConnListener Listener;

        short backlog;

        bool running, stopping;

        public EventServer(IPEndPoint listenEndPoint, short backlog)
        {
            eventBase = new EventBase();
            ListenEndPoint = listenEndPoint;
            this.backlog = backlog;
        }

        public void Dispose()
        {
			if (exitTimer != null)
				exitTimer.Dispose();
			
			if (Listener != null)
            	Listener.Dispose();
			
            eventBase.Dispose();
        }

        public void Start()
        {
            dispatch = eventBase.StartDispatchOnNewThread(BeforeDispatch, AfterDispatch);
        }

        public void Stop()
        {
            if (!running) throw new Exception("not running");
            if (stopping) throw new Exception("already stopping");

            stopping = true;
        }

        void BeforeDispatch()
        {
            if (Starting != null)
                Starting(this, EventArgs.Empty);

            Listener = new EVConnListener(eventBase, ListenEndPoint, backlog);
            Listener.ConnectionAccepted += ListenerConnectionAccepted;

            // something of a hack because we don't want to enable locking.
            exitTimer = EVEvent.CreateTimer(eventBase, true);
            exitTimer.Add(TimeSpan.FromSeconds(1));
            exitTimer.Activated += ExitTimerActivated;

            running = true;

            if (Started != null)
                Started(this, EventArgs.Empty);
        }

        void ExitTimerActivated(object sender, EventArgs e)
        {
            //Console.WriteLine("Exit timer activated.");
            if (stopping)
            {
                if (Stopping != null)
                    Stopping(this, EventArgs.Empty);

                exitTimer.Delete();
                Listener.Disable();
            }
        }

        void AfterDispatch()
        {
            exitTimer.Activated -= ExitTimerActivated;
            exitTimer.Delete();
            exitTimer.Dispose();
            exitTimer = null;

            Listener.ConnectionAccepted -= ListenerConnectionAccepted;
            Listener.Dispose();
            Listener = null;

            if (Stopped != null)
                Stopped(this, EventArgs.Empty);
        }

        void ListenerConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {
            if (ConnectionAccepted != null)
                ConnectionAccepted(this, new ConnectionEventArgs(
                    new Connection(eventBase, e.Socket, e.RemoteEndPoint)));
        }
    }

    /// <summary>
    /// A TCP connection that supports an eventful network stream. Don't forget to call Dispose()!
    /// </summary>
    public class Connection : IDisposable
    {
        IntPtr socket;
        EventBase eventBase;
        EventStream stream;

        public IPEndPoint RemoteEndPoint { get; private set; }

        internal Connection(EventBase eventBase, IntPtr socket, IPEndPoint remoteEndPoint)
        {
            this.eventBase = eventBase;
            this.socket = socket;
            RemoteEndPoint = remoteEndPoint;
        }

        public void Dispose()
        {
            socket.Close();
        }

        /// <summary>
        /// Returns an EventStream schudeled on the EventBase of the server that generated the connection.
        /// Don't forget to dispose it!
        /// </summary>
        public EventStream GetStream()
        {
            if (stream == null)
                stream = new EventStream(eventBase, socket, FileAccess.ReadWrite);

            return stream;
        }
    }

    public class ConnectionEventArgs : EventArgs
    {
        public Connection Connection { get; private set; }

        internal ConnectionEventArgs(Connection c)
        {
            Connection = c;
        }
    }
}
