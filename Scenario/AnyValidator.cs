using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Scenario
{
	public class AnyValidator : GroupValidator<HashSet<Validator>>
	{
		#region Validator implementation
		public override ValidatorState State
		{
			get
			{
				switch(state)
				{
					case ValidatorState.ENABLED:
					case ValidatorState.HIGHLIGHTED:
						bool validated = false;

						foreach(Validator v in children)
						{
							if(v == null || v.State == ValidatorState.FAILED)
							{
								State = ValidatorState.FAILED;

								validated = false;

								break;
							}
							else if(v.State == ValidatorState.VALIDATED)
							{
								validated = true;
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
				foreach(Validator v in children)
				{
					v.State = value;
				}

				state = value;
			}
		}

		public override void Notify(Validator validator, ValidatorState state)
		{
			Validate();
		}

		public override void ComputeScore(out float score, out float weight)
		{
			foreach(Validator v in children)
			{
				if(v != null && v.State == ValidatorState.VALIDATED)
				{
					v.ComputeScore(out score, out weight);

					return;
				}
			}

			score = 0;
			weight = 1;
		}
		#endregion
	}
}
