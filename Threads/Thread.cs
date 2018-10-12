using System;

#if UNITY_WSA && !UNITY_EDITOR
// On UWP platforms, threads are not available. Therefore, we need support for Tasks, i.e. .Net version >= 4
using InternalThread = System.Threading.Tasks.Task;
#else
using InternalThread = System.Threading.Thread;
#endif

namespace CLARTE.Threads
{
    public class Thread
    {
        #region Members
#if !UNITY_WSA || UNITY_EDITOR
        protected static int? mainThreadID;
#endif
        protected InternalThread thread;
        #endregion

        #region Constructors
#if !UNITY_WSA || UNITY_EDITOR
        static Thread()
        {
            mainThreadID = InternalThread.CurrentThread.ManagedThreadId;
        }
#endif

        public Thread(Action start)
        {
#if UNITY_WSA && !UNITY_EDITOR
            thread = new InternalThread(start, System.Threading.Tasks.TaskCreationOptions.LongRunning);
#else
            thread = new InternalThread(new System.Threading.ThreadStart(start));
#endif
        }
        #endregion

        #region Public methods
        public void Start()
        {
            if(thread != null)
            {
                thread.Start();
            }
        }

        public void Join()
        {
            if(thread != null)
            {
#if UNITY_WSA && !UNITY_EDITOR
                thread.Wait();
#else
                thread.Join();
#endif
            }
        }

        public static bool IsMainThread
        {
            get
            {
#if UNITY_WSA && !UNITY_EDITOR
                return !InternalThread.CurrentId.hasValue;
#else
                return (InternalThread.CurrentThread.ManagedThreadId == mainThreadID);
#endif
            }
        }
        #endregion
    }
}
