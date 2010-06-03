using System;
using System.Threading;
using Oars.Core;

namespace Oars
{
    public static class Extensions
    {
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
