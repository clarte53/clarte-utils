﻿using UnityEngine;

namespace CLARTE.Debug
{
	/// <summary>
	/// Utility script to easily inspect in debugger the values of internal variables
	/// of any MonoBehaviour objects in a scene.
	/// </summary>
	/// <remarks>This component can easily be added at runtime to debug specific objects.</remarks>
	public class Inspect : MonoBehaviour
	{
		/// <summary>
		/// The MonoBehaviour to inspect in the debugger.
		/// </summary>
		public MonoBehaviour inspected;

		private void Update()
		{
			if(inspected != null)
			{
				inspected.GetType(); // Break here to inspect object value in debugger
			}
		}
	}
}
