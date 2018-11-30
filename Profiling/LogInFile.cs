using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Profiling
{
	static public class LogInFile
	{
		static private Dictionary<string, List<string>> m_logs;

		/// <summary>
		/// Add line to the log
		/// </summary>
		/// <param name="filename">Log file name</param>
		/// <param name="log">Line to be added</param>
		/// <param name="writeImmediately">Should the log be immediately dumped into the file? Otherwise dump will only occur upon manual call to DumpLog() or DumpAllLogs()</param>
		static public void AddToLog(string filename, System.Object log, bool writeImmediately = false)
		{
			if(m_logs == null)
			{
				m_logs = new Dictionary<string, List<string>>();
			}

			if(!m_logs.ContainsKey(filename))
			{
				m_logs.Add(filename, new List<string>());

				System.IO.File.Delete(filename);
			}

			string logStr = log.ToString();

			m_logs[filename].Add(logStr);

			if(writeImmediately)
			{
				using(System.IO.StreamWriter file = new System.IO.StreamWriter(filename, true))
				{
					file.WriteLine(logStr);
				}
			}
		}

		/// <summary>
		/// Dump specific log ti file
		/// </summary>
		/// <param name="filename"></param>
		static public void DumpLog(string filename)
		{
			if(m_logs != null)
			{
				if(m_logs.ContainsKey(filename))
				{
					System.IO.File.WriteAllLines(filename, m_logs[filename].ToArray());

					UnityEngine.Debug.Log("Log dumped to " + filename);
				}
				else
				{
					UnityEngine.Debug.LogWarning("No log to be dumped into " + filename);
				}
			}
		}

		/// <summary>
		/// Dump All logs to files
		/// </summary>
		static public void DumpAllLogs()
		{
			if(m_logs != null)
			{
				foreach(string filename in m_logs.Keys)
				{
					DumpLog(filename);
				}
			}
		}
	}
}