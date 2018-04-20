using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#if UNITY_WSA && !UNITY_EDITOR
// On UWP platforms, threads are not available. Therefore, we need support for Tasks, i.e. .Net version >= 4
using MyThread = System.Threading.Tasks.Task;
#else
using MyThread = System.Threading.Thread;
#endif

namespace CLARTE.Threads
{
	/// <summary>
	/// A thread pool to avoid creating a new thread for every async tasks.
	/// </summary>
	public class Pool : IDisposable
	{
		protected class Task
		{
			#region Members
			public Action callback;
			public Result result;
			#endregion

			#region Constructors
			protected Task(Action func)
			{
				callback = func;
				result = new Result();
			}

			protected Task(Action func, Result res)
			{
				callback = func;
				result = res;
			}
			#endregion

			#region Public methods
			public static Task Create(Action callback)
			{
				return new Task(callback);
			}

			public static Task Create<T>(Func<T> callback)
			{
				Result<T> result = new Result<T>();

				Task task = new Task(() =>
				{
					T value = callback();

					result.Value = value;
				}, result);

				return task;
			}
			#endregion
		}

		#region Members
		protected List<MyThread> threads;
		protected Queue<Task> tasks;
		protected ManualResetEvent addEvent;
		protected ManualResetEvent stopEvent;
		protected object taskCountMutex;
		protected int taskCount; // We can not use the length of the tasks queue because when the lasts task is removed from the queue, it is still executed and WaitForTasksCompletion should continue to wait.
		protected bool disposed;
		#endregion

		#region Constructors / Destructors
		public Pool()
		{
			int nb_threads = Math.Max(Environment.ProcessorCount - 1, 1);

			threads = new List<MyThread>(nb_threads);
			tasks = new Queue<Task>();

			addEvent = new ManualResetEvent(false);
			stopEvent = new ManualResetEvent(false);

			taskCountMutex = new object();

			for (int i = 0; i < nb_threads; i++)
			{
#if UNITY_WSA && !UNITY_EDITOR
				MyThread thread = new MyThread(Worker, System.Threading.Tasks.TaskCreationOptions.LongRunning);
#else
				MyThread thread = new MyThread(Worker);
#endif

				threads.Add(thread);
			}

			foreach(MyThread thread in threads)
			{
				thread.Start();
			}
		}

		~Pool()
		{
			Dispose();
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
		//~Pool()
		//{
		//	Dispose(false);
		//}

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
			// GC.SuppressFinalize(this);
		}
		#endregion

		#region Worker
		protected void AddTask(Task task)
		{
			if(disposed)
			{
				throw new ObjectDisposedException("ThreadPool", "The thread pool is already disposed.");
			}

			if(task != null)
			{
				lock(taskCountMutex)
				{
					taskCount++;
				}

				lock(tasks)
				{
					tasks.Enqueue(task);
				}

				addEvent.Set();
			}
		}

		protected void Worker()
		{
			WaitHandle[] wait = new[] { addEvent, stopEvent };

			while(WaitHandle.WaitAny(wait) == 0)
			{
				Task task = null;

				lock(tasks)
				{
					if(tasks.Count > 0)
					{
						task = tasks.Dequeue();
					}
					else
					{
						// Nothing to do anymore, go to sleep
						addEvent.Reset();
					}
				}

				if(task != null && task.callback != null)
				{
					Exception exception = null;

					try
					{
						task.callback();
					}
					catch(Exception e)
					{
						exception = e;
					}

					lock(taskCountMutex)
					{
						taskCount--;
					}

					task.result.MarkAsFinished(exception);
				}
			}
		}
		#endregion

		#region Public methods
		/// <summary>
		/// Add a new task to be executed asynchronously.
		/// </summary>
		/// <param name="task">A method (task) that does not return any value.</param>
		/// <returns>A helper class to be notified when the task is complete.</returns>
		public Result AddTask(Action task)
		{
			Task t = Task.Create(task);

			if(t != null)
			{
				AddTask(t);

				return t.result;
			}

			return null;
		}

		/// <summary>
		/// Add a new task to be executed asynchronously.
		/// </summary>
		/// <typeparam name="T">The type of the returned value.</typeparam>
		/// <param name="task">A method (task) that does return a value.</param>
		/// <returns>A helper class to be notified when the task is complete and get the returned value.</returns>
		public Result<T> AddTask<T>(Func<T> task)
		{
			Task t = Task.Create(task);

			if(t != null)
			{
				AddTask(t);

				return (Result<T>) t.result;
			}

			return null;
		}

		/// <summary>
		/// Get the number of tasks currentlty planned or executing.
		/// </summary>
		/// <returns>The number of tasks.</returns>
		public long TaskCount()
		{
			if(disposed)
			{
				throw new ObjectDisposedException("ThreadPool", "The thread pool is already disposed.");
			}

			long count;

			lock(taskCountMutex)
			{
				count = taskCount;
			}

			return count;
		}

		/// <summary>
		/// Wait for all tasks (planned or executing) to complete. This is a barrier instruction.
		/// </summary>
		/// <returns>An enumerator that will return null as long as some tasks are present.</returns>
		public IEnumerator WaitForTasksCompletion()
		{
			if(disposed)
			{
				throw new ObjectDisposedException("ThreadPool", "The thread pool is already disposed.");
			}

			while(TaskCount() > 0)
			{
				yield return null;
			}
		}

		/// <summary>
		/// Utility method to execute a task and store the result in an array at a given index.
		/// </summary>
		/// <typeparam name="T">The type of the task return value.</typeparam>
		/// <param name="array">The array where to store the result.</param>
		/// <param name="index">The index at which store the result.</param>
		/// <param name="callback">The task to execute.</param>
		public void ExecAndSaveToArray<T>(T[] array, int index, Func<T> callback)
		{
			if(index >= 0 && index < array.Length)
			{
				AddTask(() => array[index] = callback());
			}
			else
			{
				throw new IndexOutOfRangeException(string.Format("Index '{0}' is not a valid index. Accepted values are [0:{1}].", index, array.Length - 1));
			}
		}
		#endregion
	}
}
