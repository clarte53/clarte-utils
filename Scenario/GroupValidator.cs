using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Scenario
{
	public abstract class GroupValidator<T> : Validator where T : ICollection<Validator>, new()
	{
		#region Members
		protected T children;
		#endregion

		#region MonoBehaviour callbacks
		protected override void Awake()
		{
			children = new T();

			foreach(Transform t in transform)
			{
				Validator v = t?.GetComponent<Validator>();

				if(v != null)
				{
					children.Add(v);
				}
			}

			base.Awake();
		}
		#endregion
	}
}
