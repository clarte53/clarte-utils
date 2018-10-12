using System;
using System.Collections.Generic;

namespace CLARTE.Threads.APC
{
	public class MonoBehaviourCall : Pattern.Singleton<MonoBehaviourCall>, ICall
	{
		#region Members
		protected System.Threading.Thread unityThread;
		protected Queue<Task> pending = new Queue<Task>();
		protected Queue<Task> inProgress = new Queue<Task>();
		#endregion
		
		#region MonoBehaviour callbacks
		protected void Awake()
		{
			unityThread = System.Threading.Thread.CurrentThread;
		}
		
		protected override void OnDestroy()
		{
			unityThread = null;
			
			pending.Clear();
			inProgress.Clear();
			
			base.OnDestroy();
		}
		
		protected void Update()
		{
			lock(pending)
			{
				while(pending.Count > 0)
				{
					inProgress.Enqueue(pending.Dequeue());
				}
			}
			
			while(inProgress.Count > 0)
			{
                inProgress.Dequeue().callback();
			}
		}
		#endregion
		
		#region ICall implementation
		public Result<T> Call<T>(Func<T> callback)
		{
            Task task = Task.Create(callback);

            Call(task);

            return (Result<T>) task.result;
        }
		
		public Result Call(Action callback)
		{
            Task task = Task.Create(callback);

            Call(task);

            return task.result;
		}
        #endregion

        #region Internal methods
        protected void Call(Task task)
        {
            if(System.Threading.Thread.CurrentThread != unityThread)
            {
                lock(pending)
                {
                    pending.Enqueue(task);
                }
            }
            else
            {
                task.callback();
            }
        }
        #endregion
    }
}
