using System.Collections.Generic;

namespace CLARTE.Scenario
{
	public class SequenceValidator : GroupValidator<List<Validator>>
	{
		#region Validator implementation
		public override void Notify(Validator validator, ValidatorState state)
		{
			throw new System.NotImplementedException();
		}

		protected override void ComputeScore(out float score, out float weight)
		{
			throw new System.NotImplementedException();
		}

		protected override ValidatorState GetState()
		{
			throw new System.NotImplementedException();
		}
		#endregion
	}
}
