using System;
using System.Collections.Generic;
using UnityEngine;
using CLARTE.Rendering.Highlight;

namespace CLARTE.Scenario
{
	[RequireComponent(typeof(Collider))]
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(IHighlight))]
	public class ZoneValidator : Validator
	{
		public abstract class Match : ScriptableObject
		{
			public abstract bool IsMatching(ZoneValidator validator, Collider other);
		}

		protected class Point
		{
			#region Members
			protected Vector3 position;
			protected Quaternion rotation;
			protected Vector3 scale;
			protected ushort notMovingCount;
			protected ushort notMovingThreshold;
			#endregion

			#region Constructors
			public Point(Transform t, float time_not_moving)
			{
				Transform = t;
				notMovingCount = 0;
				notMovingThreshold = (ushort) (time_not_moving / Time.fixedDeltaTime);
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
			public bool IsNotMoving(Transform t)
			{
				float dp = (position - t.position).sqrMagnitude;
				float dr = Quaternion.Angle(rotation, t.rotation);
				float ds = (scale - t.lossyScale).sqrMagnitude;

				if(dp > Vector3.kEpsilon || dr > Quaternion.kEpsilon || ds > Vector3.kEpsilon)
				{
					notMovingCount = 0;
				}
				else
				{
					notMovingCount++;
				}

				return notMovingCount >= notMovingThreshold;
			}
			#endregion
		}

		#region Members
		public Match match;
		[Range(0.1f, 10f)]
		public float timeNotMoving = 1f; // In seconds

		protected Dictionary<Collider, Point> inZone;
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
			if(IsEnabled())
			{
				inZone.Add(other, new Point(other.transform, timeNotMoving));
			}
		}

		protected void OnTriggerExit(Collider other)
		{
			inZone.Remove(other);
		}

		protected void OnTriggerStay(Collider other)
		{
			if(IsEnabled() && match != null && match.IsMatching(this, other) && inZone.TryGetValue(other, out Point p))
			{
				if(p.IsNotMoving(other.transform))
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

				GetComponent<IHighlight>()?.SetHighlightEnabled(state == ValidatorState.HIGHLIGHTED);
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
