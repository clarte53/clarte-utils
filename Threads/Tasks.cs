using System;

namespace CLARTE.Threads
{
	/// <summary>
	/// Helper class providing a global thread pool for the application.
	/// </summary>
	public static class Tasks
	{
		#region Members
		private static readonly Pool threads = new Pool();
		#endregion

		#region Public methods
		/// <summary>
		/// Add a new task to be executed asynchronously.
		/// </summary>
		/// <param name="task">A method (task) that does not return any value.</param>
		/// <returns>A helper class to be notified when the task is complete.</returns>
		public static Result Add(Action task)
		{
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
			return threads.AddTask(task);
		}
		#endregion
	}
}
