using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

#if UNITY_WSA && !UNITY_EDITOR
// On UWP platforms, threads are not available. Therefore, we need support for Tasks, i.e. .Net version >= 4
using MyThread = System.Threading.Tasks.Task;
#else
using MyThread = System.Threading.Thread;
#endif

namespace CLARTE.Threads
{
    public class Workers : IDisposable
    {
        #region Members
        protected List<MyThread> threads;
        protected ManualResetEvent stopEvent;
        protected bool disposed;
        #endregion

        #region Constructors / Destructors
        /// <summary>
		/// Create a new group of worker threads.
		/// </summary>
		/// <param name="worker_body">A method executed by each worker thread in an infinite loop. This method get the event that started the new iteration as parameter.</param>
        /// <param name="events">A set of events to wait for before doing the next iteration of the worker loop.</param>
        /// <param name="nb_threads">The number of worker threads to span.</param>
        public void Init(Action<WaitHandle> worker_body, ICollection<WaitHandle> events = null, uint nb_threads = 0)
        {
            if(disposed)
            {
                throw new ObjectDisposedException("CLARTE.Threads.Workers", "The thread group is already disposed.");
            }

            if(threads == null)
            {
                if(nb_threads == 0)
                {
                    nb_threads = (uint)Math.Max(Environment.ProcessorCount - 1, 1);
                }

                threads = new List<MyThread>((int)nb_threads);

                stopEvent = new ManualResetEvent(false);

                for(int i = 0; i < nb_threads; i++)
                {
#if UNITY_WSA && !UNITY_EDITOR
				    MyThread thread = new MyThread(() => Worker(worker_body, events), System.Threading.Tasks.TaskCreationOptions.LongRunning);
#else
                    MyThread thread = new MyThread(() => Worker(worker_body, events));
#endif

                    threads.Add(thread);
                }

                foreach(MyThread thread in threads)
                {
                    thread.Start();
                }
            }
        }
        #endregion

        #region IDisposable implementation
        protected virtual void Dispose(bool disposing)
        {
            if(!disposed)
            {
                if(disposing)
                {
                    // TODO: delete managed state (managed objects).

                    if(threads != null && threads.Count > 0)
                    {
                        stopEvent.Set();

                        foreach(MyThread thread in threads)
                        {
#if UNITY_WSA && !UNITY_EDITOR
							thread.Wait();
#else
                            thread.Join();
#endif
                        }

                        threads.Clear();
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                disposed = true;
            }
        }

        // TODO: replace finalizer only if the above Dispose(bool disposing) function as code to free unmanaged resources.
        ~Workers()
        {
            Dispose(/*false*/);
        }

        /// <summary>
        /// Dispose of the thread pool. Wait for curently executing async task to complete and release all the allocated threads.
        /// </summary>
        /// <remarks>Note that async tasks that are planned but not started yet will be discarded.</remarks>
        public void Dispose()
        {
            // Pass true in dispose method to clean managed resources too and say GC to skip finalize in next line.
            Dispose(true);

            // If dispose is called already then say GC to skip finalize on this instance.
            // TODO: uncomment next line if finalizer is replaced above.
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Worker
        protected void Worker(Action<WaitHandle> worker_body, ICollection<WaitHandle> events = null)
        {
            uint events_count = 1;
            int event_idx = 0;

            if(events != null)
            {
                events_count += (uint) events.Count(x => x != null);
            }

            WaitHandle[] wait = new WaitHandle[events_count];

            wait[event_idx++] = stopEvent;

            if(events != null)
            {
                foreach(WaitHandle ev in events)
                {
                    if(ev != null)
                    {
                        wait[event_idx++] = ev;
                    }
                }
            }

            while((event_idx = WaitHandle.WaitAny(wait)) != 0)
            {
                worker_body(wait[event_idx]);
            }
        }
        #endregion
    }
}
