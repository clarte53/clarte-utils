using System.Collections.Generic;
using System.Diagnostics;

namespace CLARTE.Dev.Profiling
{
	static public class Profiler
    {
		static private Dictionary<string, List<double>> durations;

		static private Dictionary<string, Stopwatch> chronos;

		/// <summary>
		/// Set the beginning of a profiling probe
		/// (i.e. start the chronometer)
		/// </summary>
		/// <param name="label">Identifier of the profiling probe</param>
		static public void Start(string label)
		{
			if (durations == null)
			{
				durations = new Dictionary<string, List<double>>();
			}

			if (!durations.ContainsKey(label))
			{
				durations.Add(label, new List<double>());
			}

			if (chronos == null)
			{
				chronos = new Dictionary<string, Stopwatch>();
			}

			if (!chronos.ContainsKey(label))
			{
				chronos.Add(label, new Stopwatch());
			}

			chronos[label].Reset();

			chronos[label].Start();
		}

		/// <summary>
		/// Sets the end of a profiling probe
		/// (i.e. stop the chronometer)
		/// </summary>
		/// <param name="label"></param>
		/// <returns>Time elapsed since Start call (ms)</returns>
		static public double Stop(string label)
		{
			if (chronos.ContainsKey(label) && durations.ContainsKey(label))
			{
				chronos[label].Stop();

				long elapsed_ticks = chronos[label].ElapsedTicks;

				double elapsed_s = (double)elapsed_ticks / (double)Stopwatch.Frequency;

				double elapsed_ms = elapsed_s * 1000.0;

				durations[label].Add(elapsed_ms);

				return elapsed_ms;
			}
			else
			{
				UnityEngine.Debug.LogError("Please call StartProfiling before StopProfiling");

				return 0.0;
			}
		}

		/// <summary>
		/// Display average duration for every profiling probe
		/// </summary>
		static public void DisplayAllAverages()
		{
			if (durations != null)
			{
				foreach (string label in durations.Keys)
				{
					DisplayAverage(label);
				}
			}
		}

		/// <summary>
		/// Display average duration for one specific profiling probe
		/// </summary>
		/// <param name="label">Identifier of the probe to be displayed</param>
		static public void DisplayAverage(string label)
		{
			if (durations != null)
			{
				if (durations.ContainsKey(label))
				{
					double sum = 0.0;

					foreach (double duration in durations[label])
					{
						sum += duration;
					}

					UnityEngine.Debug.Log("Average duration for " + label + ": " + sum / (double)durations[label].Count + "ms");
				}
				else
				{
					UnityEngine.Debug.LogWarning("Profiler not found (" + label + ")");
				}
			}
		}
	}
}