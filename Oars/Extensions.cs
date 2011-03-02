using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;

namespace Oars
{
    public static class Trace
    {
        public static void Write(string format, params object[] args)
        {
            //StackTrace stackTrace = new StackTrace();
            //StackFrame stackFrame = stackTrace.GetFrame(1);
            //MethodBase methodBase = stackFrame.GetMethod();
            //Console.WriteLine("[thread " + Thread.CurrentThread.ManagedThreadId + ", " + methodBase.DeclaringType.Name + "." + methodBase.Name + "] " + string.Format(format, args));
        }
    }

    public static class Extensions
    {
        public static void HandleException(string handlerName, Exception e)
        {
            Console.WriteLine("Exception during " + handlerName);
            Console.WriteLine("[{0}] {1}\n{2}", e.GetType().Name, e.Message, e.StackTrace);
        }

        public static Thread StartDispatchOnNewThread(this EventBase eventBase)
        {
            return eventBase.StartDispatchOnNewThread(null);
        }

        public static Thread StartDispatchOnNewThread(this EventBase eventbase, Action before)
        {
            return eventbase.StartDispatchOnNewThread(before, null);
        }

        public static Thread StartDispatchOnNewThread(this EventBase eventBase, Action before, Action after)
        {
            var dispatch = new Thread(new ThreadStart(() =>
            {
                if (before != null)
                    before();

                eventBase.Dispatch();

                if (after != null)
                    after();
            }));
            dispatch.Start();
            return dispatch;
        }

        public static Thread StartLoopOnNewThread(this EventBase eventBase, Action before, Func<bool> preLoop, Func<bool> postLoop, Action after)
        {
            var dispatch = new Thread(new ThreadStart(() =>
            {
                if (before != null)
                    before();

                while (true)
                {
                    if (preLoop())
                        break;

                    eventBase.Loop(LoopOptions.NonBlock | LoopOptions.Once);

                    if (postLoop())
                        break;
                }

                if (after != null)
                    after();
                }));
            return dispatch;
        }
    }
}
