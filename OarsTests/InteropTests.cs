using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars;
using Oars.Core;

namespace OarsTests
{
    [TestFixture]
    public class TimevalTests
    {
        [Test]
        public void FromTimespan()
        {
            var ts = new TimeSpan(0, 0, 0, 4, 20);
            var tv = timeval.FromTimeSpan(ts);

            Assert.AreEqual(ts.Seconds, tv.tv_sec);
            Assert.AreEqual(ts.Milliseconds, tv.tv_usec / 1000);
        }

        [Test]
        public void ToTimespan()
        {
            var tv = new timeval { tv_sec = 4, tv_usec = 20000 };
            var ts = tv.ToTimeSpan();

            Assert.AreEqual(tv.tv_sec, ts.Seconds);
            Assert.AreEqual(tv.tv_usec, ts.Milliseconds * 1000);
        }
    }
}
