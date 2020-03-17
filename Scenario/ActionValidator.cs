using System;

namespace CLARTE.Scenario
{
	public class ActionValidator : Validator
	{
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

		#region Public methods
		public override void Validate()
		{
			State = ValidatorState.VALIDATED;

			base.Validate();
		}
		#endregion
	}
}
