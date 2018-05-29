﻿using UnityEngine;

namespace CLARTE.Geometry.Extensions
{
	public static class QuaternionExtension
	{
		public static bool IsValid(this Quaternion quaternion)
		{
			if(float.IsNaN(quaternion.x) || float.IsNaN(quaternion.y) || float.IsNaN(quaternion.z) || float.IsNaN(quaternion.w))
				return false;
			else
				return true;
		}

		static public Vector3 QuaternionAxis(this Quaternion quaternion)
		{
			float sin = 1;

			if(quaternion.w < 1)
			{
				sin = 1 / Mathf.Sqrt(1 - quaternion.w * quaternion.w);
			}
			else
			{
				UnityEngine.Debug.LogWarning("One the rotations was identity. Computation may be wrong.");
			}

			return new Vector3(quaternion.x * sin, quaternion.y * sin, quaternion.z * sin);
		}
	}
}