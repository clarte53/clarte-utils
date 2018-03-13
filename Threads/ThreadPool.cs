using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#if UNITY_WSA
// On UWP platforms, threads are not available. Therefore, we need support for Tasks, i.e. .Net version >= 4
using MyThread = System.Threading.Tasks.Task;
#else
using MyThread = System.Threading.Thread;
#endif

namespace CLARTE.Threads
{
	public class Pool : IDisposable
	{
		protected class Task
		{
			public Action callback;
			public Result result;

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
#if UNITY_WSA
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
		public void Dispose()
		{
			if(disposed)
			{
				return;
			}

			if(threads != null && threads.Count > 0)
			{
				stopEvent.Set();

				foreach(MyThread thread in threads)
				{
#if UNITY_WSA
					thread.Wait();
#else
					thread.Join();
#endif
				}

				threads.Clear();
			}

			disposed = true;
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
						Debug.LogErrorFormat("An exception '{0}' occured in parallel jobs: {1}\n{2}", e.GetType(), e.Message, e.StackTrace);

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
		internal Result AddTask(Action task)
		{
			Task t = Task.Create(task);

			if(t != null)
			{
				AddTask(t);

				return t.result;
			}

			return null;
		}

		internal Result<T> AddTask<T>(Func<T> task)
		{
			Task t = Task.Create(task);

			if(t != null)
			{
				AddTask(t);

				return (Result<T>) t.result;
			}

			return null;
		}

		internal long TaskCount()
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

		internal IEnumerator WaitForTasksCompletion()
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

		internal void ExecAndSaveToArray<T>(T[] array, int index, Func<T> callback)
		{
			if(index >= 0 && index < array.Length)
			{
				AddTask(() => array[index] = callback());
			}
		}
		#endregion
	}
}
