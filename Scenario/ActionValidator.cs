﻿using System;
using UnityEngine;
using CLARTE.Rendering.Highlight;

namespace CLARTE.Scenario
{
	[RequireComponent(typeof(IHighlight))]
	public class ActionValidator : Validator
	{
		#region Validator implementation
		protected override void OnStateChanged(ValidatorState state)
		{
			GetComponent<IHighlight>()?.SetHighlightEnabled(state == ValidatorState.HIGHLIGHTED);
		}

		protected override void RefreshState(ValidatorState state)
		{

		}

		protected override void ComputeScore(out float score, out float weight)
		{
			score = State == ValidatorState.VALIDATED ? 1 : 0;
			weight = 1;
		}
		#endregion

		#region Public methods
		public void Validate()
		{
			SetState(ValidatorState.VALIDATED);
		}

		public void Reset()
		{
			SetState(Previous);
		}
		#endregion
	}
}
