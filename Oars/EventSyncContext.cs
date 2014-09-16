using System.Threading;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Oars
{
  public sealed class EventSyncContext : SynchronizationContext
  {
        readonly BlockingCollection<KeyValuePair<SendOrPostCallback,object>> m_queue = 
            new BlockingCollection<KeyValuePair<SendOrPostCallback,object>>();

        EventBase eventBase = new EventBase();

        public EventBase EventBase
        {
            get
            {
                return eventBase;
            }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            m_queue.Add(new KeyValuePair<SendOrPostCallback,object>(d, state));
        }

        public void RunOnCurrentThread()
        {
            KeyValuePair<SendOrPostCallback, object> workItem;
            while (true)
            {
                while (m_queue.TryTake(out workItem, 1))
                    workItem.Key(workItem.Value);
                eventBase.Loop(LoopOptions.NonBlock);
            }
        }

        public void Complete() {
            m_queue.CompleteAdding();
        }
    }
}

