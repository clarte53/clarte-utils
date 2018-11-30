using System;
using CLARTE.Pattern;

namespace CLARTE.Threads.APC
{
	public class MonoBehaviourCall : Singleton<MonoBehaviourCall>, ICall
	{
        #region Members
        protected Reactor reactor = new Reactor();
        #endregion

        #region MonoBehaviour callbacks
        protected void Update()
		{
            reactor.Update();
        }
        #endregion

        #region Public methods
        public Result<T> Call<T>(Func<T> callback)
		{
            Task task = Task.Create(callback);

            reactor.Add(task, false);

            return (Result<T>) task.result;
        }

        public Result Call(Action callback)
		{
            Task task = Task.Create(callback);

            reactor.Add(task, false);

            return task.result;
		}
        #endregion
    }
}
