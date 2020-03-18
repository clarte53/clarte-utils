using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Scenario
{
	public class SequenceValidator : GroupValidator<List<Validator>>
	{
		public enum Type
		{
			STRICT,
			SCORE_CONSTANT_PENALTY,
			SCORE_DISTANCE_PENALTY
		}

		#region Members
		public Type type;
		[Range(0f, 1f)]
		public float constantPenaltyFactor = 0.5f;

		protected List<bool> validated;
		protected List<int> validatedIndexes;
		protected int current;
		#endregion

		#region MonoBehaviour callbacks
		protected override void Awake()
		{
			base.Awake();

			validated = new List<bool>(children.Count);
			validatedIndexes = new List<int>(children.Count);

			Reset();
		}
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
						switch(type)
						{
							case Type.STRICT:
								while(current < children.Count && children[current].State == ValidatorState.VALIDATED)
								{
									validatedIndexes.Add(current);

									current++;

									if(current < children.Count)
									{
										children[current].State = state;
									}
								}

								if(current >= children.Count)
								{
									state = ValidatorState.VALIDATED;
								}

								break;
							default:
								int count = children.Count;

								for(int i = 0; i < count; i++)
								{
									if(children[i].State == ValidatorState.VALIDATED && !validated[i])
									{
										validated[i] = true;
										validatedIndexes.Add(i);
									}
								}

								if(validatedIndexes.Count == children.Count)
								{
									state = ValidatorState.VALIDATED;
								}

								break;
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

				Reset();

				switch(state)
				{
					case ValidatorState.ENABLED:
					case ValidatorState.HIGHLIGHTED:
						foreach(Validator v in children)
						{
							v.State = type == Type.STRICT ? ValidatorState.DISABLED : state;
						}

						if(current < children.Count)
						{
							children[current].State = state;
						}

						break;
					default:
						foreach(Validator v in children)
						{
							v.State = state;
						}

						break;
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

			int count = children.Count;
			int nb_validated = validatedIndexes.Count;

			for(int i = 0; i < count; i++)
			{
				children[i].ComputeScore(out float child_score, out float child_weight);

				weight += child_weight;

				if(i < nb_validated)
				{
					if(validatedIndexes[i] == i)
					{
						score += child_score;
					}
					else
					{
						switch(type)
						{
							case Type.SCORE_CONSTANT_PENALTY:
								score += child_score * constantPenaltyFactor;
								break;
							case Type.SCORE_DISTANCE_PENALTY:
								score += child_score / (1 + Mathf.Abs(validatedIndexes[i] - i));
								break;
							default:
								break; //In strict mode, errors have a score of 0
						}
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

		#region Internal methods
		protected void Reset()
		{
			validated.Clear();
			validatedIndexes.Clear();

			current = 0;

			foreach(Validator v in children)
			{
				validated.Add(false);
			}
		}
		#endregion
	}
}
