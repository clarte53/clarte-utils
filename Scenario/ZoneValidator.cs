using System;
using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Scenario
{
	[RequireComponent(typeof(Collider))]
	[RequireComponent(typeof(Rigidbody))]
	public abstract class ZoneValidator : Validator
	{
		protected class Point
		{
			#region Members
			public Vector3 position;
			public Quaternion rotation;
			public Vector3 scale;
			#endregion

			#region Constructors
			public Point(Transform t)
			{
				Transform = t;
			}
			#endregion

			#region Getters / Setters
			public Transform Transform
			{
				set
				{
					position = value.position;
					rotation = value.rotation;
					scale = value.lossyScale;
				}
			}
			#endregion

			#region Pubplic methods
			public bool IsMoving(Transform t)
			{
				float dp = (position - t.position).sqrMagnitude;
				float dr = Quaternion.Angle(rotation, t.rotation);
				float ds = (scale - t.lossyScale).sqrMagnitude;

				return dp > Vector3.kEpsilon || dr > Quaternion.kEpsilon || ds > Vector3.kEpsilon;
			}
			#endregion
		}

		#region Members
		protected Dictionary<Collider, Point> inZone;
		#endregion

		#region Abstract methods
		protected abstract bool Match(Collider collider);
		#endregion

		#region MonoBehaviour callbacks
		protected void Awake()
		{
			inZone = new Dictionary<Collider, Point>();

			GetComponent<Collider>().isTrigger = true;
			GetComponent<Rigidbody>().isKinematic = true; ;
		}

		protected void OnTriggerEnter(Collider other)
		{
			if(IsEnabled() && Match(other))
			{
				inZone.Add(other, new Point(other.transform));
			}
		}

		protected void OnTriggerExit(Collider other)
		{
			inZone.Remove(other);
		}

		protected void OnTriggerStay(Collider other)
		{
			if(IsEnabled() && inZone.TryGetValue(other, out Point p))
			{
				if(!p.IsMoving(other.transform))
				{
					State = ValidatorState.VALIDATED;

					inZone.Remove(other);

					Validate();
				}

				p.Transform = other.transform;
			}
		}
		#endregion

		#region Validator implementation
		public override ValidatorState State
		{
			get
			{
				return state;
			}

			set
			{
				state = value;
			}
		}

		public override void Notify(Validator validator, ValidatorState state)
		{
			throw new InvalidOperationException(string.Format("Validators of type '{0}' are supposed to be terminal elements and should not hav children.", GetType()));
		}

		public override void ComputeScore(out float score, out float weight)
		{
			score = State == ValidatorState.VALIDATED ? 1 : 0;
			weight = 1;
		}
		#endregion

		#region Internal methods
		protected bool IsEnabled()
		{
			return State == ValidatorState.ENABLED || State == ValidatorState.HIGHLIGHTED;
		}
		#endregion
	}
}
