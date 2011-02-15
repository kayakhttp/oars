using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Oars.Core;
using System.Concurrency;
using System.Disposables;

namespace Oars
{
    class EventBaseScheduler : IScheduler
    {
        EventBase eventBase;

        public DateTimeOffset Now
        {
            get { return new DateTimeOffset(eventBase.GetTimeOfDayCached()); }
        }

        public IDisposable Schedule(Action action, TimeSpan dueTime)
        {
            var ev = EVEvent.CreateTimer(eventBase, false);
            ev.Add(dueTime);
            return Disposable.Create(() => ev.Dispose());
        }

        public IDisposable Schedule(Action action)
        {
            return Schedule(action, TimeSpan.Zero);
        }
    }

    class OarsServer
    {
        EventBase eventBase;

        public OarsServer(EventBase eventBase)
        {
            this.eventBase = eventBase;
        }

        public void Start(IPEndPoint listenEndPoint, short backlog)
        {
            eventBase = new EventBase();
            
            var listener = new EVConnListener(eventBase, listenEndPoint, backlog);
            listener.Enable();
            listener.ConnectionAccepted = HandleRequest;

            eventBase.Dispatch();

            listener.Dispose();

        }

        public void HandleRequest(IntPtr fd, IPEndPoint ep)
        {
        }
    }

    interface IOarsApp
    {
        void ProcessSocket(BufferEvent bev);
    }

    class BufferEventObservable
    {
    }
}
