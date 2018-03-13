using System;

namespace CLARTE.Threads
{
	public static class Tasks
	{
		private static readonly Pool threads = new Pool();

		public static Result Add(Action task)
		{
			return threads.AddTask(task);
		}

		public static Result<T> Add<T>(Func<T> task)
		{
			return threads.AddTask(task);
		}
	}
}
