using System;
using System.Collections.Generic;
using UnityEngine;
using CLARTE.Rendering.Highlight;

namespace CLARTE.Scenario
{
	[RequireComponent(typeof(Collider))]
	[RequireComponent(typeof(Rigidbody))]
	[RequireComponent(typeof(IHighlight))]
	public class ZoneValidator : ActionValidator
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
		protected Collider matching;
		#endregion

		#region MonoBehaviour callbacks
		protected override void Awake()
		{
			base.Awake();

			inZone = new Dictionary<Collider, Point>();

			GetComponent<Collider>().isTrigger = true;
			GetComponent<Rigidbody>().isKinematic = true; ;
		}

		protected void OnTriggerEnter(Collider other)
		{
			inZone.Add(other, new Point(other.transform, timeNotMoving));
		}

		protected void OnTriggerExit(Collider other)
		{
			if(State == ValidatorState.VALIDATED && other == matching && inZone.TryGetValue(other, out Point p))
			{
				matching = null;

				Reset();
			}

			inZone.Remove(other);
		}

		protected void OnTriggerStay(Collider other)
		{
			if(IsEnabled() && inZone.TryGetValue(other, out Point p))
			{
				if(match != null && match.IsMatching(this, other) && p.IsNotMoving(other.transform))
				{
					matching = other;

					Validate();
				}
				else if(State == ValidatorState.VALIDATED)
				{
					matching = null;

					Reset();
				}

				p.Transform = other.transform;
			}
		}
		#endregion

		#region Internal methods
		protected bool IsEnabled()
		{
			ValidatorState state = State;

			return state == ValidatorState.ENABLED || state == ValidatorState.HIGHLIGHTED || state == ValidatorState.VALIDATED;
		}
		#endregion
	}
}
