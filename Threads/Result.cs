using System;

namespace CLARTE.Threads
{
	/// <summary>
	/// Async future result. Results provide feeback of async task completion and access to eventualy raised exceptions.
	/// </summary>
	public class Result
	{
		#region Members
		protected bool done = false;
		protected Exception exception = null;
		#endregion

		#region Getter / Setter
		/// <summary>
		/// Check whether the task is done or not.
		/// </summary>
		/// <remarks>The call to this property is NOT blocking. Therefore it can be used to check periodically for the task completion.</remarks>
		/// <returns>True if the task is done, false otherwise.</returns>
		public bool Done
		{
			get
			{
				lock(this)
				{
					return done;
				}
			}
		}

		/// <summary>
		/// Check whether the task raised an uncatched exception or not.
		/// </summary>
		/// <remarks>The call to this property is blocking until the task is complete.</remarks>
		/// <returns>True if no exception where raised, false otherwise.</returns>
		public bool Success
		{
			get
			{
				return (Exception == null);
			}
		}

		/// <summary>
		/// Get any uncatched exception raised by the task.
		/// </summary>
		/// <remarks>The call to this property is blocking until the task is complete.</remarks>
		/// <returns>The uncatched exception raised by the task, or null otherwise.</returns>
		public Exception Exception
		{
			get
			{
				// Block until the task finished or failed
				Wait();

				lock(this)
				{
					return exception;
				}
			}
		}

		/// <summary>
		/// Mark the task as completed. Never call this method yourself!
		/// </summary>
		public void MarkAsFinished(Exception raised = null)
		{
			lock(this)
			{
				done = true;

				exception = raised;
			}
		}
		#endregion

		#region Utility methods
		/// <summary>
		/// Wait for the task to complete.
		/// </summary>
		/// <remarks>The call to this property is blocking until the task is complete.</remarks>
		public void Wait()
		{
			while(!Done)
			{ }
		}
		#endregion
	}

	/// <summary>
	/// Specialized async result to get the return value of an async task.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	public class Result<T> : Result
	{
		#region Members
		protected T result;
		#endregion

		#region Getter / Setter
		/// <summary>
		/// Get or set the return value of the task. The value is automatically set when the task is complete.
		/// </summary>
		/// <remarks>The call to this property is blocking until the task is complete.</remarks>
		/// <returns>The return value of the task.</returns>
		public T Value
		{
			get
			{
				// Block until a value gets available
				Wait();

				lock(this)
				{
					return result;
				}
			}

			set
			{
				lock(this)
				{
					result = value;
				}
			}
		}
		#endregion
	}
}
