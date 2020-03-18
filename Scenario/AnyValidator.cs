using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Scenario
{
	public class AnyValidator : GroupValidator<HashSet<Validator>>
	{
		#region Members
		protected Validator validated;
		#endregion

		#region Validator implementation
		public override ValidatorState State
		{
			get
			{
				switch(state)
				{
					case ValidatorState.ENABLED:
					case ValidatorState.HIGHLIGHTED:
						validated = null;

						foreach(Validator v in children)
						{
							if(v == null || v.State == ValidatorState.FAILED)
							{
								State = ValidatorState.FAILED;

								validated = null;

								break;
							}
							else if(v.State == ValidatorState.VALIDATED && validated == null)
							{
								validated = v;
							}
						}

						if(validated != null)
						{
							Validator v = validated;

							State = ValidatorState.VALIDATED;

							validated = v;
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

				validated = null;

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
			if(validated != null && validated.State == ValidatorState.VALIDATED)
			{
				validated.ComputeScore(out score, out weight);

				return;
			}

			score = 0;
			weight = 1;
		}
		#endregion
	}
}
