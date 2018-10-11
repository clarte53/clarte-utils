using System;

namespace CLARTE.Threads.APC
{
	public interface ICall
	{
		Result<T> Call<T>(Func<T> callback);

		Result Call(Action callback);
	}
}
