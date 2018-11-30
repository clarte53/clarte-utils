using System.Collections.Generic;
using CLARTE.Threads;

namespace CLARTE.Pattern
{
    public class Reactor
    {
        protected struct Context
        {
            #region Members
            public Task task;
            public bool wait;
            #endregion

            #region Constructors
            public Context(Task task, bool wait)
            {
                this.task = task;
                this.wait = wait;
            }
            #endregion
        }

        #region Members
        protected Queue<Context> pending = new Queue<Context>();
        protected Queue<Context> inProgress = new Queue<Context>();
        protected Context current;
        #endregion

        #region Public methods
        public void Update()
        {
            lock(pending)
            {
                while(pending.Count > 0)
                {
                    inProgress.Enqueue(pending.Dequeue());
                }
            }

            while(inProgress.Count > 0 && (current.task == null || !current.wait || current.task.result.Done))
            {
                current = inProgress.Dequeue();

                current.task.callback();
            }
        }

        public void Add(Task task, bool wait_before_next_task)
        {
            lock(pending)
            {
                pending.Enqueue(new Context(task, wait_before_next_task));
            }
        }
        #endregion
    }
}
