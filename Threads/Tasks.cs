using System;
using UnityEngine;

namespace CLARTE.Threads
{
	/// <summary>
	/// Helper class providing a global thread pool for the application.
	/// </summary>
	public class Tasks : MonoBehaviour
	{
		#region Members
		private static Pool threads;
		#endregion

		#region Constructors
		// Create a hidden gameobject in order to get notifications when the application is
		// destroyed so we can dispose of the thread pool. Otherwise, on standalone build
		// with .Net 4.6, the application can not be closed as the threads in the pool are
		// never asked to shutdown because the finalizer of the pool is somewhat never called.
		private static void Init()
		{
			if(threads == null)
			{
				GameObject go = new GameObject("Tasks");

				go.hideFlags = HideFlags.HideAndDontSave;

				go.AddComponent<Tasks>();

				threads = new Pool();
			}
		}
		#endregion

		#region MonoBehaviour callbacks
		private void OnDestroy()
		{
			if(threads != null)
			{
				threads.Dispose();

				threads = null;
			}
		}
		#endregion

		#region Public methods
		/// <summary>
		/// Add a new task to be executed asynchronously.
		/// </summary>
		/// <param name="task">A method (task) that does not return any value.</param>
		/// <returns>A helper class to be notified when the task is complete.</returns>
		public static Result Add(Action task)
		{
			Init();

			return threads.AddTask(task);
		}

		/// <summary>
		/// Add a new task to be executed asynchronously.
		/// </summary>
		/// <typeparam name="T">The type of the returned value.</typeparam>
		/// <param name="task">A method (task) that does return a value.</param>
		/// <returns>A helper class to be notified when the task is complete and get the returned value.</returns>
		public static Result<T> Add<T>(Func<T> task)
		{
			Init();

			return threads.AddTask(task);
		}
		#endregion
	}
}
