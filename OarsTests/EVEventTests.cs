using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars;

namespace OarsTests
{
    [TestFixture]
    public class EventTests
    {
        EVEvent ev1, ev2, ev3;
        EventBase eventBase;
        DateTime added, activated;
        bool ev1Activated, ev2Activated, ev3Activated;

        [SetUp]
        public void SetUp()
        {
            eventBase = new EventBase();
        }

        [TearDown]
        public void TearDown()
        {
            eventBase.Dispose();
        }

        [Test]
        public void EventTimerAdd()
        {
            var intendedDuration = TimeSpan.FromMilliseconds(500);

            ev1 = EVEvent.CreateTimer(eventBase);

            var dispatch = eventBase.StartDispatchOnNewThread(() =>
            {
                //Console.WriteLine("Setting timeout for " + intendedDuration.TotalMilliseconds);
                ev1.Add(intendedDuration);
                added = eventBase.GetTimeOfDayCached();
                //Console.WriteLine("Added event at time: " + added.Millisecond);

                ev1.Activated = EventTimerAddActivated;
            }, () => {
                ev1.Delete();
                ev1.Dispose();
            });
            
            dispatch.Join();

            Assert.IsTrue(ev1Activated, "Event was never activated.");

            var actualDuration = activated - added;
            //Console.WriteLine("Actual duration: " + actualDuration.TotalMilliseconds);
            var difference = intendedDuration - actualDuration;
            //Console.WriteLine("Difference: " + difference);
            Assert.Less(difference.TotalMilliseconds, 10, "Large discrepancy between intended and actual timeout duration.");
        }

        void EventTimerAddActivated()
        {
            //Console.WriteLine("Activated event.");
            ev1Activated = true;
            activated = eventBase.GetTimeOfDayCached();
            eventBase.LoopExit();
            ev1.Activated = null;
        }

        [Test]
        public void EventTimerDelete()
        {
            var timeout = TimeSpan.FromMilliseconds(500);
            var removeTimeout = TimeSpan.FromMilliseconds(250);
            var timeoutToBeRemoved = TimeSpan.FromMilliseconds(375);

            ev1 = EVEvent.CreateTimer(eventBase);
            ev1.Activated = Ev1Activated;
            ev2 = EVEvent.CreateTimer(eventBase);
            ev2.Activated = Ev2Activated;
            ev3 = EVEvent.CreateTimer(eventBase);
            ev3.Activated = Ev3Activated;

            var dispatch = eventBase.StartDispatchOnNewThread(() => {
                ev1.Add(timeout);
                ev2.Add(removeTimeout);
                ev3.Add(timeoutToBeRemoved);
            }, () => {
                ev1.Delete();
                ev1.Dispose();
                ev2.Delete();
                ev2.Dispose();
            });

            dispatch.Join();

            Assert.IsTrue(ev1Activated, "Event 1 did not activate.");
            Assert.IsTrue(ev2Activated, "Event 2 did not activate.");
            Assert.IsFalse(ev3Activated, "Event 3 erroneously activated after being deleted.");
        }

        // comes in first
        void Ev1Activated()
        {
            ev1Activated = true;
            ev3.Delete();
            ev3.Dispose();
        }

        // comes in second
        void Ev2Activated()
        {
            ev2Activated = true;
            eventBase.LoopExit();
        }
        
        // should never happen (would come before ev2 if event was not deleted)
        void Ev3Activated()
        {
            // should never happen
            ev3Activated = true;
        }
    }
}
