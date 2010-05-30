using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace OarsTests
{
    class Program
    {
        public static void Main(string[] args)
        {
            NUnit.ConsoleRunner.Runner.Main(new string[] { Assembly.GetExecutingAssembly().Location, "-noshadow" }.Concat(args).ToArray());
        }
    }
}
