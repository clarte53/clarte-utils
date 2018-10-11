using System;

namespace CLARTE.Threads
{
    public class Task
    {
        #region Members
        public Action callback;
        public Result result;
        #endregion

        #region Constructors
        protected Task(Action func, Result res)
        {
            callback = func;
            result = res;
        }
        #endregion

        #region Public methods
        public static Task Create(Action callback)
        {
            if(callback == null)
            {
                throw new ArgumentNullException("callback", "Invalid null callback in task.");
            }

            Result result = new Result();

            return new Task(() =>
            {
                Exception exception = null;

                try
                {
                    callback();
                }
                catch(Exception e)
                {
                    exception = e;
                }

                result.Complete(exception);
            }, result);
        }

        public static Task Create<T>(Func<T> callback)
        {
            if(callback == null)
            {
                throw new ArgumentNullException("callback", "Invalid null callback in task.");
            }

            Result<T> result = new Result<T>();

            return new Task(() =>
            {
                Exception exception = null;

                try
                {
                    result.Value = callback();
                }
                catch(Exception e)
                {
                    exception = e;
                }

                result.Complete(exception);
            }, result);
        }
        #endregion
    }
}
