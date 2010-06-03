using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars;
using System.Net;
using System.Threading;
using Oars.Core;

namespace OarsTests
{
    [TestFixture]
    public class EventBaseTests
    {
        static int ListenPort;

        EventBase eventBase;

        [SetUp]
        public void Setup()
        {
            eventBase = new EventBase();
        }

        [TearDown]
        public void TearDown()
        {
            eventBase.Dispose();
        }

        [Test]
        public void ExitTest()
        {
            AddEvent();

            bool gotExit = false;
            var dispatch = new Thread(new ThreadStart(() =>
            {
                eventBase.Dispatch();
                gotExit = eventBase.GotExit;
            }));

            dispatch.Start();

            // wait for the dispatch thread to start.
            Thread.Sleep(TimeSpan.FromSeconds(.1));

            Assert.IsTrue(eventBase.LoopExit(TimeSpan.FromSeconds(1)), "LoopExit failed.");

            // wait for exit
            dispatch.Join();

            Assert.IsTrue(gotExit, "EventBase.GotExit was false even though LoopExit() was called.");
        }

        [Test]
        public void ExitTestNegative()
        {
            AddEvent();

            bool gotExit = false;
            var dispatch = new Thread(new ThreadStart(() =>
            {
                eventBase.Dispatch();
                gotExit = eventBase.GotExit;
            }));
            dispatch.Start();

            // wait for the dispatch thread to start.
            Thread.Sleep(TimeSpan.FromSeconds(.1));

            Assert.IsTrue(eventBase.LoopBreak(), "LoopBreak failed.");

            // wait for exit
            dispatch.Join();

            Assert.IsFalse(gotExit, "EventBase.GotExit was true even though LoopExit() was NOT called.");
        }

        [Test]
        public void BreakTest()
        {
            AddEvent();

            bool gotBreak = false;
            var dispatch = new Thread(new ThreadStart(() =>
            {
                eventBase.Dispatch();
                gotBreak = eventBase.GotBreak;
            }));
            dispatch.Start();

            // wait for the dispatch thread to start.
            Thread.Sleep(TimeSpan.FromSeconds(.1));

            Assert.IsTrue(eventBase.LoopBreak(), "LoopBreak failed.");

            // wait for exit
            dispatch.Join();

            Assert.IsTrue(gotBreak, "EventBase.GotBreak was false even though LoopBreak() was called.");
        }

        void AddEvent()
        {
            EVEvent timer = EVEvent.CreateTimer(eventBase);
            timer.Add(TimeSpan.FromSeconds(1));
        }
    }
}
