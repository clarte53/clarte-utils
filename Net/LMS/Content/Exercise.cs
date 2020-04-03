using System;
using UnityEngine;

namespace CLARTE.Net.LMS.Content
{
	public abstract class Exercise : MonoBehaviour
	{
		#region Abstract methods
		public abstract Module Module { get; }
		public abstract Guid Guid { get; }
		public abstract string Name { get; }
		public abstract byte Level { get; }
		#endregion

		#region MonoBehaviour callbacks
		protected virtual void Awake()
		{
			FindObjectOfType<Client>()?.RegisterExercise(this);
		}
		#endregion
	}
}
