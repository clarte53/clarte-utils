using UnityEngine;

namespace CLARTE.Serialization
{
	/// <summary>
	/// Binary serializer. It provide a fast and memory efficient way to serialize data into binary representation.
	/// </summary>
	/// <remarks>This class is pure C# and is compatible with all platforms, including hololens.</remarks>
	public partial class Binary
	{
		#region Convert from bytes
		/// <summary>
		/// Deserialize a Vector2 value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out Vector2 value)
		{
			float x, y;

			uint read = FromBytes(buffer, start, out x);
			read += FromBytes(buffer, start + read, out y);

			value = new Vector2(x, y);

			return read;
		}

		/// <summary>
		/// Deserialize a Vector3 value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out Vector3 value)
		{
			float x, y, z;

			uint read = FromBytes(buffer, start, out x);
			read += FromBytes(buffer, start + read, out y);
			read += FromBytes(buffer, start + read, out z);

			value = new Vector3(x, y, z);

			return read;
		}

		/// <summary>
		/// Deserialize a Vector4 value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out Vector4 value)
		{
			float w, x, y, z;

			uint read = FromBytes(buffer, start, out x);
			read += FromBytes(buffer, start + read, out y);
			read += FromBytes(buffer, start + read, out z);
			read += FromBytes(buffer, start + read, out w);

			value = new Vector4(x, y, z, w);

			return read;
		}

		/// <summary>
		/// Deserialize a Quaternion value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out Quaternion value)
		{
			float w, x, y, z;

			uint read = FromBytes(buffer, start, out x);
			read += FromBytes(buffer, start + read, out y);
			read += FromBytes(buffer, start + read, out z);
			read += FromBytes(buffer, start + read, out w);

			value = new Quaternion(x, y, z, w);

			return read;
		}

		/// <summary>
		/// Deserialize a Color value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out Color value)
		{
			byte r, g, b, a;

			uint read = FromBytes(buffer, start, out r);
			read += FromBytes(buffer, start + read, out g);
			read += FromBytes(buffer, start + read, out b);
			read += FromBytes(buffer, start + read, out a);

			value = new Color32(r, g, b, a);

			return read;
		}
		#endregion

		#region Convert to bytes
		/// <summary>
		/// Serialize a Vector2 value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, Vector2 value)
		{
			uint written = ToBytes(ref buffer, start, value.x);
			written += ToBytes(ref buffer, start + written, value.y);

			return written;
		}

		/// <summary>
		/// Serialize a Vector3 value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, Vector3 value)
		{
			uint written = ToBytes(ref buffer, start, value.x);
			written += ToBytes(ref buffer, start + written, value.y);
			written += ToBytes(ref buffer, start + written, value.z);

			return written;
		}

		/// <summary>
		/// Serialize a Vector4 value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, Vector4 value)
		{
			uint written = ToBytes(ref buffer, start, value.x);
			written += ToBytes(ref buffer, start + written, value.y);
			written += ToBytes(ref buffer, start + written, value.z);
			written += ToBytes(ref buffer, start + written, value.w);

			return written;
		}

		/// <summary>
		/// Serialize a Quaternion value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, Quaternion value)
		{
			uint written = ToBytes(ref buffer, start, value.x);
			written += ToBytes(ref buffer, start + written, value.y);
			written += ToBytes(ref buffer, start + written, value.z);
			written += ToBytes(ref buffer, start + written, value.w);

			return written;
		}

		/// <summary>
		/// Serialize a Color value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, Color value)
		{
			Color32 val = value;

			uint written = ToBytes(ref buffer, start, val.r);
			written += ToBytes(ref buffer, start + written, val.g);
			written += ToBytes(ref buffer, start + written, val.b);
			written += ToBytes(ref buffer, start + written, val.a);

			return written;
		}
		#endregion
	}
}
