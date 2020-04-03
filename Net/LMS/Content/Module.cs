using System;
using UnityEngine;

namespace CLARTE.Net.LMS.Content
{
	public abstract class Module : MonoBehaviour
	{
		#region Abstract methods
		public abstract Application Application { get; }
		public abstract Guid Guid { get; }
		public abstract string Name { get; }
		#endregion

		#region MonoBehaviour callbacks
		protected virtual void Awake()
		{
			FindObjectOfType<Client>()?.RegisterModule(this);
		}
		#endregion
	}
}
