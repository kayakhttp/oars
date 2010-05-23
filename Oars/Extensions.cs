using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Oars.Core;

namespace Oars
{
    public static class Extensions
    {
        public static Thread StartDispatchOnNewThread(this EventBase eventBase)
        {
            var dispatch = new Thread(new ThreadStart(() =>
            {
                eventBase.Dispatch();
            }));
            dispatch.Start();
            return dispatch;
        }
    }
}
