using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Oars.Core;
using Oars;
using System.IO;

namespace OarsTests
{
    [TestFixture]
    public class BufferEventStreamTests
    {
        string readFile = "readTest.dat", writeFile = "writeTest.dat";

        EventBase eventBase;
        EventStream stream;

        int bytesRead;
        bool gotReadCallback, gotWriteCallback;
        byte[] readBuffer;
        MemoryStream readData;


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
        public void TestRead()
        {
            var testString = MakeTestData();
            File.WriteAllText(readFile, testString);

            // correct me if i'm wrong but i think
            // all chars [A-Z0-9] are 1 byte in utf8
            var readData = new byte[testString.Length];

            var file = File.OpenRead(readFile);
            var dispatch = eventBase.StartDispatchOnNewThread(() =>
            {
                stream = new EventStream(eventBase, file.Handle, FileAccess.Read);
                stream.BeginRead(readData, 0, readData.Length, ReadCallback, null);
            }, () =>
            {
                file.Close();
                stream.Dispose();
            });

            dispatch.Join();

            Assert.IsTrue(gotReadCallback, "Never got read callback.");
            Assert.AreEqual(testString.Length, bytesRead, "Test data length and number of bytes read by stream differ.");

            var readString = Encoding.UTF8.GetString(readData);
            Assert.AreEqual(testString, readString, "Test data and data read by stream differ.");

            //Console.WriteLine("Read test done.");
        }

        [Test]
        public void TestWrite()
        {
            var testString = MakeTestData();
            byte[] writeData = Encoding.UTF8.GetBytes(testString);

            var file = File.OpenWrite(writeFile);
            var dispatch = eventBase.StartDispatchOnNewThread(() =>
            {
                stream = new EventStream(eventBase, file.Handle, FileAccess.Write);
                stream.BeginWrite(writeData, 0, writeData.Length, WriteCallback, null);
            }, () =>
            {
                file.Close();
                stream.Dispose();
            });

            dispatch.Join();

            Assert.IsTrue(gotWriteCallback, "Never got write callback.");
            Assert.AreEqual(writeData.Length, new FileInfo(writeFile).Length, "Test data length and size of written file differ.");

            var writtenString = File.ReadAllText(writeFile);
            //Console.WriteLine("written string = " + writtenString);
            Assert.AreEqual(testString, writtenString, "Test data and data written by stream differ");
        }

        void ReadCallback(IAsyncResult iasr)
        {
            //Console.WriteLine("Got read callback.");
            gotReadCallback = true;
            bytesRead = stream.EndRead(iasr);
            eventBase.LoopExit();
        }

        void WriteCallback(IAsyncResult iasr)
        {
            //Console.WriteLine("Got write callback.");
            gotWriteCallback = true;
            stream.EndWrite(iasr);
            eventBase.LoopExit();
        }

        static char[] chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
        static Random random = new Random();

        string MakeTestData()
        {
            return new string(DataGenerator().Take(1024 * 85).ToArray());
        }

        IEnumerable<char> DataGenerator()
        {
            while (true)
                yield return chars.ElementAt(random.Next(chars.Length));
        }
    }
}
