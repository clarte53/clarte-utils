using System;
using System.Collections;
using System.Threading.Tasks;

namespace CLARTE.Serialization
{
	/// <summary>
	/// Binary serializer. It provide a fast and memory efficient way to serialize data into binary representation.
	/// </summary>
	/// <remarks>This class is pure C# and is compatible with all platforms, including hololens.</remarks>
	public partial class Binary
	{
		/// <summary>
		/// Delegate used to pass user defined serialization logic.
		/// </summary>
		/// <param name="serializer">The serializer to use.</param>
		/// <param name="buffer">The buffer to use.</param>
		/// <returns>The number of bytes written.</returns>
		public delegate uint SerializationCallback(Binary serializer, ref Buffer buffer);

		/// <summary>
		/// Delegate used to pass user defined deserialization logic.
		/// </summary>
		/// <param name="serializer">The serializer to use.</param>
		/// <param name="buffer">The buffer to use.</param>
		/// <returns>The number of bytes read.</returns>
		public delegate uint DeserializationCallback(Binary serializer, Buffer buffer);

		/// <summary>
		/// Helper struct used for wrapping serialized / deserialized value when using simple one value serialization logic. 
		/// </summary>
		protected class DefaultSerializationCallbacks
		{
			#region Members
			/// <summary>
			/// The serialized / deserialized value.
			/// </summary>
			public object data;
			#endregion

			#region Constructors
			/// <summary>
			/// Constructor.
			/// </summary>
			public DefaultSerializationCallbacks()
			{
				data = null;
			}

			/// <summary>
			/// Constructor.
			/// </summary>
			/// <param name="data">The value to serialize.</param>
			public DefaultSerializationCallbacks(object data)
			{
				this.data = data;
			}
			#endregion

			#region Public methods
			/// <summary>
			/// Callback used to pass simple one value serialization logic.
			/// </summary>
			/// <param name="serializer">The serializer to use.</param>
			/// <param name="buffer">The buffer to use.</param>
			/// <returns>The number of bytes written.</returns>
			public uint SerializationCallback(Binary serializer, ref Buffer buffer)
			{
				return serializer != null && buffer != null ? serializer.ToBytes(ref buffer, 0, data) : 0;
			}

			/// <summary>
			/// Callback used to pass simple one value deserialization logic.
			/// </summary>
			/// <param name="serializer">The serializer to use.</param>
			/// <param name="buffer">The buffer to use.</param>
			/// <returns>The number of bytes read.</returns>
			public uint DeserializationCallback(Binary serializer, Buffer buffer)
			{
				data = null;

				return serializer.FromBytes(buffer, 0, out data);
			}
			#endregion
		}

		#region Members
		/// <summary>
		/// Serialization buffer of 10 Mo by default.
		/// </summary>
		public const uint defaultSerializationBufferSize = 1024 * 1024 * 10;

		private static readonly TimeSpan progressRefresRate = new TimeSpan(0, 0, 0, 0, 40);
		#endregion

		#region Public serialization methods
		/// <summary>
		/// Serialize an object to a file asynchronously, using user defined logic.
		/// </summary>
		/// <param name="serialization_callback">The callback used to serialize the data once the context is set.</param>
		/// <param name="filename">The name of the file where to save the serialized data.</param>
		/// <param name="callback">A callback called once the data is serialized to know if the serialization was a success.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>An enumerator to wait for the serialization completion.</returns>
		public IEnumerator Serialize(SerializationCallback serialization_callback, string filename, Action<bool> callback = null, Action<float> progress = null, uint default_buffer_size = defaultSerializationBufferSize)
		{
			return Serialize(serialization_callback, (b, s) =>
			{
				bool success = false;

				if(b != null && b.Length > 0)
				{
					using(System.IO.FileStream fs = System.IO.File.Open(filename, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
					{
						fs.Write(b, 0, (int) s);
					}

					success = true;
				}

				if(callback != null)
				{
					callback(success);
				}
			}, progress, default_buffer_size);
		}

		/// <summary>
		/// Serialize an object to a file asynchronously.
		/// </summary>
		/// <param name="value">The value to serialize.</param>
		/// <param name="filename">The name of the file where to save the serialized data.</param>
		/// <param name="callback">A callback called once the data is serialized to know if the serialization was a success.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>An enumerator to wait for the serialization completion.</returns>
		public IEnumerator Serialize(object value, string filename, Action<bool> callback = null, Action<float> progress = null, uint default_buffer_size = defaultSerializationBufferSize)
		{
			return Serialize(value, (b, s) =>
			{
				bool success = false;

				if(b != null && b.Length > 0)
				{
					using(System.IO.FileStream fs = System.IO.File.Open(filename, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
					{
						fs.Write(b, 0, (int) s);
					}

					success = true;
				}

				if(callback != null)
				{
					callback(success);
				}
			}, progress, default_buffer_size);
		}

		/// <summary>
		/// Serialize an object to a byte array asynchronously, using user defined logic.
		/// </summary>
		/// <param name="serialization_callback">The callback used to serialize the data once the context is set.</param>
		/// <param name="callback">A callback called once the data is serialized to get the result byte array and serialized size.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>An enumerator to wait for the serialization completion.</returns>
		public IEnumerator Serialize(SerializationCallback serialization_callback, Action<byte[], uint> callback, Action<float> progress = null, uint default_buffer_size = defaultSerializationBufferSize)
		{
			Buffer buffer = null;

			try
			{
				DateTime time = DateTime.Now + progressRefresRate;
				float progress_percentage = 0f;

				buffer = GetBuffer(default_buffer_size, p => progress_percentage = p);

				Task<uint> result = Task.Run(() => serialization_callback(this, ref buffer));

				while(!result.IsCompleted)
				{
					if(progress != null && DateTime.Now >= time)
					{
						progress(progress_percentage);

						time = DateTime.Now + progressRefresRate;
					}

					yield return null;
				}

				if(result.Exception != null)
				{
					throw new SerializationException("An error occured during serialization.", result.Exception);
				}

				if(callback != null)
				{
					callback(buffer.Data, result.Result);
				}
			}
			finally
			{
				if(buffer != null)
				{
					buffer.Dispose();
				}
			}
		}

		/// <summary>
		/// Serialize an object to a byte array asynchronously.
		/// </summary>
		/// <param name="value">The value to serialize.</param>
		/// <param name="callback">A callback called once the data is serialized to get the result byte array and serialized size.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>An enumerator to wait for the serialization completion.</returns>
		public IEnumerator Serialize(object value, Action<byte[], uint> callback, Action<float> progress = null, uint default_buffer_size = defaultSerializationBufferSize)
		{
			DefaultSerializationCallbacks context = new DefaultSerializationCallbacks(value);

			IEnumerator it = Serialize(context.SerializationCallback, callback, progress, default_buffer_size);

			while(it.MoveNext())
			{
				yield return it.Current;
			}
		}

		/// <summary>
		/// Serialize objects to a byte array synchronously, using user defined logic.
		/// </summary>
		/// <param name="serialization_callback">The callback used to serialize the data once the context is set.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>The serialized data.</returns>
		public byte[] Serialize(SerializationCallback serialization_callback, uint default_buffer_size = defaultSerializationBufferSize)
		{
			byte[] result = null;

			Buffer buffer = null;

			try
			{
				buffer = GetBuffer(default_buffer_size);

				uint written = serialization_callback(this, ref buffer);

				result = new byte[written];

				Array.Copy(buffer.Data, result, written);
			}
			catch(Exception e)
			{
				throw new SerializationException("An error occured during serialization.", e);
			}
			finally
			{
				if(buffer != null)
				{
					buffer.Dispose();
				}
			}

			return result;
		}

		/// <summary>
		/// Serialize an object to a byte array synchronously.
		/// </summary>
		/// <param name="value">The value to serialize.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>The serialized data.</returns>
		public byte[] Serialize(object value, uint default_buffer_size = defaultSerializationBufferSize)
		{
			DefaultSerializationCallbacks context = new DefaultSerializationCallbacks(value);

			return Serialize(context.SerializationCallback, default_buffer_size);
		}

		/// <summary>
		/// Deserialize an object from a file asynchronously, using user defined logic.
		/// </summary>
		/// <param name="filename">The name of the file where to get the deserialized data.</param>
		/// /// <param name="deserialization_callback">The callback used to deserialize the data once the context is set.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <returns>An enumerator to wait for the deserialization completion.</returns>
		public IEnumerator Deserialize(string filename, DeserializationCallback deserialization_callback, Action<float> progress = null)
		{
			byte[] data = System.IO.File.ReadAllBytes(filename);

			return Deserialize(data, deserialization_callback, progress);
		}

		/// <summary>
		/// Deserialize an object from a file asynchronously.
		/// </summary>
		/// <param name="filename">The name of the file where to get the deserialized data.</param>
		/// <param name="callback">A callback to get the deserialized object.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <returns>An enumerator to wait for the deserialization completion.</returns>
		public IEnumerator Deserialize(string filename, Action<object> callback, Action<float> progress = null)
		{
			byte[] data = System.IO.File.ReadAllBytes(filename);

			return Deserialize(data, callback, progress);
		}

		/// <summary>
		/// Deserialize an object from a byte array asynchronously, using user defined logic.
		/// </summary>
		/// <param name="data">The byte array containing the serialized data.</param>
		/// <param name="deserialization_callback">The callback used to deserialize the data once the context is set.</param>
		/// <param name="callback">A callback to get the deserialized object.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <returns>An enumerator to wait for the deserialization completion.</returns>
		public IEnumerator Deserialize(byte[] data, DeserializationCallback deserialization_callback, Action<float> progress = null)
		{
			DateTime time = DateTime.Now + progressRefresRate;
			float progress_percentage = 0f;

			using(Buffer buffer = GetBufferFromExistingData(data, p => progress_percentage = p))
			{
				Task<uint> result = Task.Run(() => deserialization_callback(this, buffer));

				while(!result.IsCompleted)
				{
					if(progress != null && DateTime.Now >= time)
					{
						progress(progress_percentage);

						time = DateTime.Now + progressRefresRate;
					}

					yield return null;
				}

				if(result.Exception != null)
				{
					throw new DeserializationException("An error occured during deserialization.", result.Exception);
				}
				else if(result.Result != data.Length)
				{
					throw new DeserializationException("Invalid deserialization. Not all available data was used.", null);
				}
			}
		}

		/// <summary>
		/// Deserialize an object from a byte array asynchronously.
		/// </summary>
		/// <param name="data">The byte array containing the serialized data.</param>
		/// <param name="callback">A callback to get the deserialized object.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <returns>An enumerator to wait for the deserialization completion.</returns>
		public IEnumerator Deserialize(byte[] data, Action<object> callback, Action<float> progress = null)
		{
			DefaultSerializationCallbacks context = new DefaultSerializationCallbacks();

			IEnumerator it = Deserialize(data, context.DeserializationCallback, progress);

			while(it.MoveNext())
			{
				yield return it.Current;
			}

			if(callback != null)
			{
				callback(context.data);
			}
		}

		/// <summary>
		/// Deserialize an object from a byte array synchronously, using user defined logic.
		/// </summary>
		/// <param name="data">The byte array containing the serialized data.</param>
		/// <param name="deserialization_callback">The callback used to deserialize the data once the context is set.</param>
		public void Deserialize(byte[] data, DeserializationCallback deserialization_callback)
		{
			using(Buffer buffer = GetBufferFromExistingData(data))
			{
				try
				{
					uint read = deserialization_callback(this, buffer);

					if(read != data.Length)
					{
						throw new DeserializationException("Not all available data was used.", null);
					}
				}
				catch(Exception e)
				{
					throw new DeserializationException("An error occured during deserialization.", e);
				}
			}
		}

		/// <summary>
		/// Deserialize an object from a byte array synchronously.
		/// </summary>
		/// <param name="data">The byte array containing the serialized data.</param>
		/// <returns>The deserialized object.</returns>
		public object Deserialize(byte[] data)
		{
			DefaultSerializationCallbacks context = new DefaultSerializationCallbacks();

			Deserialize(data, context.DeserializationCallback);

			return context.data;
		}
		#endregion
	}
}
