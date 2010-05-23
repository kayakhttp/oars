using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using Oars.Tests;

namespace Oars
{
    class Program
    {
        public static short Backlog = 1000;
        public static short Port = 9182;

        public static void Main(string[] args)
        {
            ITest test;
            
            test = new SocketStreamTest();
            //test = new BasicTest();
            test.Run();
        }
    }
}
