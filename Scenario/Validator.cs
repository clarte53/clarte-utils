using UnityEngine;

namespace CLARTE.Scenario
{
	public abstract class Validator : MonoBehaviour
	{
		#region Members
		protected ValidatorState state;
		private float score;
		private float scoreWeight;
		#endregion

		#region Abstract methods
		public abstract ValidatorState State { get; set; }
		public abstract void Notify(Validator validator, ValidatorState state);
		public abstract void ComputeScore(out float score, out float weight);
		#endregion

		#region Getters / Setters
		public float Score
		{
			get
			{
				return score;
			}
		}

		public float ScoreWeight
		{
			get
			{
				return scoreWeight;
			}
		}
		#endregion

		#region Public methods
		public void Validate()
		{
			ComputeScore(out score, out scoreWeight);

			transform.parent.GetComponent<Validator>()?.Notify(this, State);
		}
		#endregion
	}
}
