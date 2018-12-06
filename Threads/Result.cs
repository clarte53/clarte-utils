using System;
using System.Threading;
using UnityEngine;

namespace CLARTE.Threads
{
	/// <summary>
	/// Async future result. Results provide feeback of async task completion and access to eventualy raised exceptions.
	/// </summary>
	public class Result : IDisposable
	{
        #region Members
        protected ManualResetEvent waitHandle = new ManualResetEvent(false);
        protected Action callback;
        protected Exception exception;
        protected bool exceptionChecked;
        protected bool disposed;
        #endregion

        #region Constructors
        /// <summary>
        /// Create a new future result.
        /// </summary>
        /// <param name="callback">An optional callback to call when result is completed.</param>
        public Result(Action callback = null)
        {
            this.callback = callback;
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

                    // If we got an exception and the user did not checked it, we display an error message before the info is lost.
                    if(!exceptionChecked && exception != null)
                    {
                        Debug.LogErrorFormat("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
                    }
#if NETFX_CORE
                    waitHandle.Dispose();
#else
                    waitHandle.Close();
#endif
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                disposed = true;
            }
        }

        // TODO: replace finalizer only if the above Dispose(bool disposing) function as code to free unmanaged resources.
        ~Result()
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
                return waitHandle.WaitOne(0);
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

				exceptionChecked = true;

				return exception;
			}
		}

		/// <summary>
		/// Mark the task as completed. Never call this method yourself!
		/// </summary>
		public void Complete(Exception raised = null)
		{
			exception = raised;

            waitHandle.Set();

            if(callback != null)
            {
                callback();
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
            waitHandle.WaitOne();
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

        #region Constructors
        /// <summary>
        /// Create a new future result.
        /// </summary>
        /// <param name="callback">An optional callback to call when result is completed.</param>
        public Result(Action callback = null) : base(callback)
        {

        }
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

				return result;
			}

			set
			{
				result = value;
			}
		}
		#endregion
	}
}
