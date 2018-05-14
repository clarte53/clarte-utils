using UnityEngine;

namespace CLARTE.Geometry.Extensions
{
	public static class TransformExtension
	{
		public static void SetPosition(this Transform transform, Vector3 position, Transform referential = null)
		{
			if(referential != null)
			{
				transform.position = referential.TransformPoint(position);
			}
			else
			{
				transform.position = position;
			}
		}

		public static Vector3 GetPosition(this Transform transform, Transform referential = null)
		{
			if(referential != null)
			{
				return referential.InverseTransformPoint(transform.position);
			}
			else
			{
				return transform.position;
			}
		}

		public static void SetOrientation(this Transform transform, Quaternion orientation, Transform referential = null)
		{
			if(referential != null)
			{
				transform.rotation = referential.rotation * orientation;
			}
			else
			{
				transform.rotation = orientation;
			}
		}

		public static Quaternion GetOrientation(this Transform transform, Transform referential = null)
		{
			if(referential != null)
			{
				return Quaternion.Inverse(referential.rotation) * transform.rotation;
			}
			else
			{
				return transform.rotation;
			}
		}

		public static Vector3 Forward(this Transform transform, Transform referential = null)
		{
			if(referential != null)
			{
				return referential.InverseTransformVector(transform.forward);
			}
			else
			{
				return transform.forward;
			}
		}

		public static Vector3 Up(this Transform transform, Transform referential = null)
		{
			if(referential != null)
			{
				return referential.InverseTransformVector(transform.up);
			}
			else
			{
				return transform.up;
			}
		}

		public static Vector3 Right(this Transform transform, Transform referential = null)
		{
			if(referential != null)
			{
				return referential.InverseTransformVector(transform.right);
			}
			else
			{
				return transform.right;
			}
		}

		public static void ShowHierarchy(this Transform transform, bool state)
		{
			Renderer[] renderers = transform.gameObject.GetComponentsInChildren<Renderer>();

			if(renderers != null)
			{
				foreach(Renderer renderer in renderers)
				{
					renderer.enabled = state;
				}
			}
		}

		public static void SetLocalMatrix(this Transform transform, Matrix4x4 matrix)
		{
			transform.localPosition = matrix.ExtractTranslation();
			transform.localRotation = matrix.ExtractRotationQuaternion();
			transform.localScale = matrix.ExtractScale();
		}

		public static Matrix4x4 GetLocalMatrix(this Transform transform)
		{
			Matrix4x4 mat = new Matrix4x4();

			mat.SetTRS(transform.localPosition, transform.localRotation, transform.localScale);

			return mat;
		}

		public static Matrix4x4 GetWorldMatrix(this Transform transform)
		{
			return transform.localToWorldMatrix;
		}

		public static void SetWorldMatrix(this Transform transform, Matrix4x4 matrix)
		{
			Matrix4x4 parentMatrix = transform.parent == null ? Matrix4x4.identity : transform.parent.localToWorldMatrix;

			transform.SetLocalMatrix(parentMatrix.inverse * matrix);
		}
	}
}
