using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace CLARTE.Threads
{
	/// <summary>
	/// A thread pool for parallel processing of identical data.
	/// </summary>
	public class ParallelProcessing<T, U> : Workers<U>, IDisposable
	{
		public delegate void Process(U context, T data);

		#region Members
		protected Process algorithm;
		protected Queue<T> tasks;
		protected ManualResetEvent addEvent;
		protected ManualResetEvent completedEvent;
		protected object taskCountMutex;
		protected int taskCount; // We can not use the length of the tasks queue because when the lasts task is removed from the queue, it is still executed and WaitForTasksCompletion should continue to wait.
		#endregion

		#region Constructors / Destructors
		/// <summary>
		/// Create a new thread pool for processing data in parallel.
		/// </summary>
		/// <param name="algorithm">Algorithm used to process the queued data.</param>
		/// <param name="context_factory">a factory method used to generate context for each of the worker threads</param>
		/// <param name="nb_threads">The number of worker threads to span. If zero, the worker is started in (nb_cpu_cores - 1) threads.</param>
		public ParallelProcessing(Process algorithm, Descriptor.ContextFactory context_factory, uint nb_threads = 0)
		{
			if (algorithm == null)
			{
				throw new ArgumentNullException("algorithm", "Invalid null algorithm method in parallel processing constructor.");
			}

			this.algorithm = algorithm;

			tasks = new Queue<T>();

			addEvent = new ManualResetEvent(false);
			completedEvent = new ManualResetEvent(true);

			taskCountMutex = new object();

			Init(new Descriptor(context_factory, Worker, new ManualResetEvent[] { addEvent }, nb_threads));
		}
		#endregion

		#region Worker
		protected void Worker(U context, WaitHandle ev)
		{
			if (ev == addEvent)
			{
				T data = default;

				bool has_data = false;

				lock (tasks)
				{
					if (tasks.Count > 0)
					{
						data = tasks.Dequeue();

						has_data = true;
					}
					else
					{
						// Nothing to do anymore, go to sleep
						addEvent.Reset();
					}
				}

				if (has_data)
				{
					algorithm(context, data);

					lock (taskCountMutex)
					{
						taskCount--;

						if(taskCount <= 0)
                        {
							completedEvent.Set();
                        }
					}
				}
			}
		}
		#endregion

		#region Public methods
		/// <summary>
		/// Add a new data to be processed asynchronously.
		/// </summary>
		/// <param name="data">The data to process.</param>
		public void AddData(T data)
		{
			if (disposed)
			{
				throw new ObjectDisposedException("CLARTE.Threads.ParallelProcessing", "The processing pool is already disposed.");
			}

			if (data != null)
			{
				lock (taskCountMutex)
				{
					taskCount++;

					completedEvent.Reset();
				}

				lock (tasks)
				{
					tasks.Enqueue(data);
				}

				addEvent.Set();
			}
		}

		/// <summary>
		/// Get the number of tasks currentlty planned or executing.
		/// </summary>
		/// <returns>The number of tasks.</returns>
		public long TaskCount()
		{
			if (disposed)
			{
				throw new ObjectDisposedException("CLARTE.Threads.ParallelProcessing", "The processing pool is already disposed.");
			}

			lock (taskCountMutex)
			{
				return taskCount;
			}
		}

		/// <summary>
		/// Wait for all tasks (planned or executing) to complete. This is a blocking barrier instruction.
		/// </summary>
		public void WaitUntilTasksCompletion()
		{
			if (disposed)
			{
				throw new ObjectDisposedException("CLARTE.Threads.ParallelProcessing", "The processing pool is already disposed.");
			}

			completedEvent.WaitOne();
		}

		/// <summary>
		/// Wait for all tasks (planned or executing) to complete. This is a non-blocking barrier instruction.
		/// </summary>
		/// <returns>An enumerator that will return null as long as some tasks are present.</returns>
		public IEnumerator WaitForTasksCompletion()
		{
			if (disposed)
			{
				throw new ObjectDisposedException("CLARTE.Threads.ParallelProcessing", "The processing pool is already disposed.");
			}

			while (TaskCount() > 0)
			{
				yield return null;
			}
		}
		#endregion
	}
}
