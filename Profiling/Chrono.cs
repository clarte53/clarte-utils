using System.Diagnostics;

namespace CLARTE.Profiling
{
	/// <summary>
	/// Tick-accurate chronometer class
	/// </summary>
	public class Chrono
	{
		private Stopwatch m_stopWatch;

		public Chrono()
		{
			m_stopWatch = new Stopwatch();
		}

		/// <summary>
		/// Start chronometer
		/// </summary>
		public void Start()
		{
			m_stopWatch.Start();
		}

		/// <summary>
		/// Stop chronometer
		/// </summary>
		public void Stop()
		{
			m_stopWatch.Stop();
		}

		/// <summary>
		/// Reset chronometer
		/// </summary>
		public void Reset()
		{
			m_stopWatch.Reset();
		}

		/// <summary>
		/// Get elapsed time in seconds
		/// </summary>
		/// <returns></returns>
		public double GetElapsedTime()
		{
			long elapsed_ticks = m_stopWatch.ElapsedTicks;
			double elapsed_s = (double)elapsed_ticks / (double)Stopwatch.Frequency;

			return elapsed_s;
		}
	}
}