using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Oars.Core;

namespace Oars.Tests
{
    class BufferEventTest : ITest
    {
        public void Run()
        {
            EventBase eventBase = new EventBase();
            EVConnListener listener = new EVConnListener(eventBase, new IPEndPoint(IPAddress.Any, Program.Port), Program.Backlog);

            listener.ConnectionAccepted += ConnectionAccepted;

            eventBase.Dispatch();

        }

        void ConnectionAccepted(object sender, ConnectionAcceptedEventArgs e)
        {
            var listener = (EVConnListener)sender;
            Console.WriteLine("Accepted a connection from " + e.RemoteEndPoint);
            BufferEvent bufferEvent = new BufferEvent(listener.Base, e.Socket);
            //bufferEvent.ReadCallback = ReadCallback;
            //bufferEvent.EventCallback = EventCallback;
            bufferEvent.Enable();
        }

        void ReadCallback(BufferEvent bufferEvent)
        {
            EVBuffer input = bufferEvent.Input;
            EVBuffer output = bufferEvent.Output;

            var buffer = new byte[16];
            var bytesRead = input.Remove(buffer, 0, buffer.Length);

            output.Add(buffer, 0, bytesRead);
        }

        void EventCallback(BufferEvent bufferEvent, BufferEventEvents events)
        {
            if ((events & BufferEventEvents.Error) > 0)
                Console.WriteLine("error!");
            if ((events & (BufferEventEvents.EOF | BufferEventEvents.Error)) > 0)
            {
                Console.WriteLine("got EOF or error");
                bufferEvent.Dispose();
            }
        }
    }

    interface ITest
    {
        void Run();
    }
}
