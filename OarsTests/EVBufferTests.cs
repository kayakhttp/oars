using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars;

namespace OarsTests
{
    [TestFixture]
    public class EVBufferTests
    {
        static byte[] testData = Encoding.UTF8.GetBytes("this is some test data!!!");

        Buffer buffer;

        [SetUp]
        public void Setup()
        {
            buffer = new Buffer();
        }

        [TearDown]
        public void TearDown()
        {
            buffer.Dispose();
        }

        [Test]
        public void Add()
        {
            buffer.Add(testData, 0, testData.Length);
            Assert.AreEqual(testData.Length, buffer.Length);
        }

        [Test]
        public void RemoveBytes()
        {
            buffer.Add(testData, 0, testData.Length);
            var data = new byte[testData.Length];
            buffer.Remove(data, 0, data.Length);

            Assert.AreEqual(Encoding.UTF8.GetString(data), Encoding.UTF8.GetString(testData));
        }

        [Test]
        public void RemoveBuffer()
        {
            buffer.Add(testData, 0, testData.Length);

            var buffer2 = new Buffer();
            buffer.Remove(buffer2, testData.Length);

            Assert.AreEqual(buffer.Length, 0);
            Assert.AreEqual(buffer2.Length, testData.Length);

            var data = new byte[testData.Length];
            buffer2.Remove(data, 0, data.Length);

            Assert.AreEqual(Encoding.UTF8.GetString(data), Encoding.UTF8.GetString(testData));
        }
    }
}
