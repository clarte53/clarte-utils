using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Scenario
{
	public class AllValidator : GroupValidator<List<Validator>>
	{
		#region Validator implementation
		public override ValidatorState State {
			get
			{
				switch(state)
				{
					case ValidatorState.ENABLED:
					case ValidatorState.HIGHLIGHTED:
						bool validated = true;

						foreach(Validator v in children)
						{
							if(v == null || v.State == ValidatorState.FAILED)
							{
								State = ValidatorState.FAILED;

								validated = false;

								break;
							}
							else if(v.State != ValidatorState.VALIDATED)
							{
								validated = false;

								break;
							}
						}

						if(validated)
						{
							State = ValidatorState.VALIDATED;
						}

						break;
					default:
						break;
				}

				return state;
			}

			set
			{
				state = value;

				foreach(Validator v in children)
				{
					v.State = state;
				}
			}
		}

		public override void Notify(Validator validator, ValidatorState state)
		{
			Validate();
		}

		public override void ComputeScore(out float score, out float weight)
		{
			score = weight = 0;

			foreach(Validator v in children)
			{
				if(v != null)
				{
					v.ComputeScore(out float child_score, out float child_weight);

					weight += child_weight;

					if(v.State == ValidatorState.VALIDATED)
					{
						score += child_score;
					}
				}
			}

			if(Mathf.Abs(weight) < Vector3.kEpsilon)
			{
				score = 0;
				weight = 1;
			}
		}
		#endregion
	}
}
