using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace CLARTE.Serialization
{
	/// <summary>
	/// Binary serializer. It provide a fast and memory efficient way to serialize data into binary representation.
	/// </summary>
	/// <remarks>This class is pure C# and is compatible with all platforms, including hololens.</remarks>
	public class Binary
	{
		/// <summary>
		/// The types that are supported natively by the serializer. Other types can be added by implementing IBinarySerializable.
		/// </summary>
		public enum SupportedTypes
		{
			BINARY_SERIALIZABLE,
			BYTE,
			BOOL,
			INT,
			UINT,
			LONG,
			ULONG,
			FLOAT,
			DOUBLE,
			STRING,
			VECTOR2,
			VECTOR3,
			VECTOR4,
			QUATERNION,
			COLOR,
			ARRAY,
			DICTIONARY,
		}

		/// <summary>
		/// Exception raised when an error happens during serialization.
		/// </summary>
		public class SerializationException : Exception
		{
			#region Constructors
			/// <summary>
			/// Constructor of serialization exception.
			/// </summary>
			/// <param name="message">Description of the error.</param>
			/// <param name="inner_exception">The exception that was raised during the serialization.</param>
			public SerializationException(string message, Exception inner_exception) : base(message, inner_exception)
			{

			}
			#endregion
		}

		/// <summary>
		/// Exception raised when an error happens during deserialization.
		/// </summary>
		public class DeserializationException : Exception
		{
			#region Constructors
			/// <summary>
			/// Constructor of deserialization exception.
			/// </summary>
			/// <param name="message">Description of the error.</param>
			/// /// <param name="inner_exception">The exception that was raised during the deserialization.</param>
			public DeserializationException(string message, Exception inner_exception) : base(message, inner_exception)
			{

			}
			#endregion
		}

		/// <summary>
		/// A buffer of bytes.
		/// </summary>
		public class Buffer : IDisposable
		{
			#region Members
			protected Binary serializer;
			protected uint resizeCount;
			protected byte[] data;
			protected Action<float> progress;
			private bool disposed = false;
			#endregion

			#region Constructors
			/// <summary>
			/// Create a new buffer of at least min_size bytes.
			/// </summary>
			/// <remarks>The buffer can potentially be bigger, depending on the available allocated resources.</remarks>
			/// <param name="manager">The associated serializer.</param>
			/// <param name="min_size">The minimal size of the buffer.</param>
			/// <param name="progress_callback">A callback to notify progress of the current task.</param>
			/// <param name="resize_count">The number of times this buffer as been resized.</param>
			public Buffer(Binary manager, uint min_size, Action<float> progress_callback, uint resize_count = 0)
			{
				serializer = manager;
				resizeCount = resize_count;
				progress = progress_callback;

				data = serializer.Grab(min_size);
			}

			/// <summary>
			/// Create a new buffer from existing data.
			/// </summary>
			/// <param name="manager">The associated serializer.</param>
			/// <param name="existing_data">The existing data.</param>
			/// <param name="progress_callback">A callback to notify progress of the current task.</param>
			public Buffer(Binary manager, byte[] existing_data, Action<float> progress_callback)
			{
				serializer = manager;
				resizeCount = 0;
				progress = progress_callback;

				data = existing_data;
			}
			#endregion

			#region Destructor
			// Make sure that internal data get released to the serializer
			~Buffer()
			{
				Dispose(true);
			}
			#endregion

			#region Getter / Setter
			/// <summary>
			/// Get the buffer bytes data.
			/// </summary>
			public byte[] Data
			{
				get
				{
					return data;
				}
			}

			/// <summary>
			/// Get the number of times this buffer has been resized.
			/// </summary>
			public uint ResizeCount
			{
				get
				{
					return resizeCount;
				}
			}

			/// <summary>
			/// Get the progress callback associated with this buffer.
			/// </summary>
			public Action<float> Progress
			{
				get
				{
					return progress;
				}
			}
			#endregion

			#region IDisposable implementation
			public virtual void Dispose(bool disposing)
			{
				if(!disposed)
				{
					if(disposing)
					{
						// TODO: delete managed state (managed objects).

						serializer.Release(data);
					}

					// TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
					// TODO: set fields of large size with null value.

					resizeCount = 0;
					serializer = null;
					data = null;
					progress = null;

					disposed = true;
				}
			}

			/// <summary>
			/// Dispose of the buffer. Release the allocated memory to the serializer for futur use.
			/// </summary>
			public void Dispose()
			{
				// Pass true in dispose method to clean managed resources too and say GC to skip finalize in next line.
				Dispose(true);

				// If dispose is called already then say GC to skip finalize on this instance.
				// TODO: uncomment next line if finalizer is replaced above.
				GC.SuppressFinalize(this);
			}
			#endregion
		}

		// Helper attribute to search with reflection for some specific methods with complex prototypes 
		private class MethodLocatorAttribute : Attribute
		{
			public enum Type
			{
				FROM_BYTES,
				TO_BYTES
			}

			public enum Parameter
			{
				ARRAY,
				DICTIONARY
			}

			#region Members
			public Type type;
			public Parameter parameter;
			#endregion

			#region Constructors
			public MethodLocatorAttribute(Type t, Parameter p)
			{
				type = t;
				parameter = p;
			}
			#endregion
		}

		// Store both int and float values at the same offset. Therefore, both fields share the same bytes
		[StructLayout(LayoutKind.Explicit)]
		private struct Converter
		{
			[FieldOffset(0)]
			public float Float1;

			[FieldOffset(sizeof(int))]
			public float Float2;

			[FieldOffset(0)]
			public double Double;

			[FieldOffset(0)]
			public int Int1;

			[FieldOffset(sizeof(int))]
			public int Int2;

			[FieldOffset(0)]
			public long Long;

			public Converter(float value1, float value2 = 0f)
			{
				Double = 0d;
				Int1 = 0;
				Int2 = 0;
				Long = 0;
				Float1 = value1;
				Float2 = value2;
			}

			public Converter(double value)
			{
				Float1 = 0f;
				Float2 = 0f;
				Int1 = 0;
				Int2 = 0;
				Long = 0;
				Double = value;
			}

			public Converter(int value1, int value2 = 0)
			{
				Float1 = 0f;
				Float2 = 0f;
				Double = 0d;
				Long = 0;
				Int1 = value1;
				Int2 = value2;
			}

			public Converter(long value)
			{
				Float1 = 0f;
				Float2 = 0f;
				Double = 0d;
				Int1 = 0;
				Int2 = 0;
				Long = value;
			}
		}

		#region Members
		/// <summary>
		/// Serialization buffer of 10 Mo by default.
		/// </summary>
		public const uint defaultSerializationBufferSize = 1024 * 1024 * 10;
		public const float minResizeOffset = 0.1f;

		protected const uint nbParameters = 3;
		protected const uint mask = 0xFF;
		protected const uint byteBits = 8;
		protected const uint byteSize = sizeof(byte);
		protected const uint intSize = sizeof(int);
		protected const uint uintSize = sizeof(uint);
		protected const uint longSize = sizeof(long);
		protected const uint ulongSize = sizeof(ulong);
		protected const uint floatSize = sizeof(float);
		protected const uint doubleSize = sizeof(double);

		private static readonly Dictionary<SupportedTypes, uint> sizes;
		private static readonly MethodInfo fromBytesArray;
		private static readonly MethodInfo fromBytesDictionary;
		private static readonly MethodInfo toBytesArray;
		private static readonly MethodInfo toBytesDictionary;
		private static readonly object[] emptyParameters;
		private static readonly bool isLittleEndian;

		private LinkedList<byte[]> available;
		#endregion

		#region Constructors
		static Binary()
		{
#pragma warning disable 0162
			if(uintSize != intSize)
			{
				throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. ({2} != {3})", "int", "uint", intSize, uintSize));
			}

			if(longSize != 2 * intSize)
			{
				throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (2 * {2} != {3})", "int", "long", intSize, longSize));
			}

			if(ulongSize != 2 * intSize)
			{
				throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (2 * {2} != {3})", "int", "ulong", intSize, ulongSize));
			}

			if(floatSize != intSize)
			{
				throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. ({2} != {3})", "int", "float", intSize, floatSize));
			}

			if(doubleSize != 2 * intSize)
			{
				throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (2 * {2} != {3})", "int", "double", intSize, doubleSize));
			}
#pragma warning restore 0162

			emptyParameters = new object[] { };

			isLittleEndian = BitConverter.IsLittleEndian;

			sizes = new Dictionary<SupportedTypes, uint>();

			sizes.Add(SupportedTypes.BYTE, byteSize);
			sizes.Add(SupportedTypes.BOOL, byteSize);
			sizes.Add(SupportedTypes.INT, intSize);
			sizes.Add(SupportedTypes.UINT, uintSize);
			sizes.Add(SupportedTypes.LONG, longSize);
			sizes.Add(SupportedTypes.ULONG, ulongSize);
			sizes.Add(SupportedTypes.FLOAT, floatSize);
			sizes.Add(SupportedTypes.DOUBLE, doubleSize);
			sizes.Add(SupportedTypes.VECTOR2, 2 * floatSize);
			sizes.Add(SupportedTypes.VECTOR3, 3 * floatSize);
			sizes.Add(SupportedTypes.VECTOR4, 4 * floatSize);
			sizes.Add(SupportedTypes.QUATERNION, 4 * floatSize);
			sizes.Add(SupportedTypes.COLOR, 4 * byteSize);

			MethodInfo[] methods = typeof(Binary).GetMethods();

			foreach(MethodInfo method in methods)
			{
				IEnumerable<object> attributes = method.GetCustomAttributes(typeof(MethodLocatorAttribute), false);

				foreach(object attribute in attributes)
				{
					if(attribute != null && attribute is MethodLocatorAttribute)
					{
						MethodLocatorAttribute locator = (MethodLocatorAttribute) attribute;

						switch(locator.type)
						{
							case MethodLocatorAttribute.Type.FROM_BYTES:
								switch(locator.parameter)
								{
									case MethodLocatorAttribute.Parameter.ARRAY:
										fromBytesArray = method;
										break;
									case MethodLocatorAttribute.Parameter.DICTIONARY:
										fromBytesDictionary = method;
										break;
									default:
										throw new NotImplementedException(string.Format("Method locator attribute parameter '{0}' not supported.", locator.parameter));
								}

								break;
							case MethodLocatorAttribute.Type.TO_BYTES:
								switch(locator.parameter)
								{
									case MethodLocatorAttribute.Parameter.ARRAY:
										toBytesArray = method;
										break;
									case MethodLocatorAttribute.Parameter.DICTIONARY:
										toBytesDictionary = method;
										break;
									default:
										throw new NotImplementedException(string.Format("Method locator attribute parameter '{0}' not supported.", locator.parameter));
								}

								break;
							default:
								throw new NotImplementedException(string.Format("Method locator attribute type '{0}' not supported.", locator.type));
						}
					}
				}
			}
		}

		public Binary()
		{
			available = new LinkedList<byte[]>();
		}
		#endregion

		#region Getter / Setter
		/// <summary>
		/// Get the size in bytes of a supported type.
		/// </summary>
		/// <param name="type">The type from which to get the size.</param>
		/// <returns>The number of bytes of the type once serialized, or 0 if unknown.</returns>
		public static uint Size(SupportedTypes type)
		{
			uint size;

			if(! sizes.TryGetValue(type, out size))
			{
				size = 0;
			}

			return size;
		}
		#endregion

		#region Public serialization methods
		/// <summary>
		/// Serialize an object to a file.
		/// </summary>
		/// <typeparam name="T">The type of the object to serialize.</typeparam>
		/// <param name="value">The value to serialize.</param>
		/// <param name="filename">The name of the file where to save the serialized data.</param>
		/// <param name="callback">A callback called once the data is serialized to know if the serialization was a success.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>An enumerator to wait for the serialization completion.</returns>
		public IEnumerator Serialize<T>(T value, string filename, Action<bool> callback = null, Action<float> progress = null, uint default_buffer_size = defaultSerializationBufferSize)
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
		/// Serialize an object to a byte array.
		/// </summary>
		/// <typeparam name="T">The type of the object to serialize.</typeparam>
		/// <param name="value">The value to serialize.</param>
		/// <param name="callback">A callback called once the data is serialized to get the result byte array and serialized size.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>An enumerator to wait for the serialization completion.</returns>
		public IEnumerator Serialize<T>(T value, Action<byte[], uint> callback, Action<float> progress = null, uint default_buffer_size = defaultSerializationBufferSize)
		{
			Buffer buffer = null;

			try
			{
				float progress_percentage = 0f;

				buffer = GetBuffer(default_buffer_size, p => progress_percentage = p);

				SupportedTypes type = GetSupportedType(typeof(T));

				Threads.Result<uint> result = Threads.Tasks.Add(() => ToBytesWrapper(ref buffer, 0, value, type));

				while(!result.Done)
				{
					if(progress != null)
					{
						progress(progress_percentage);
					}

					yield return null;
				}

				if(result.Exception != null)
				{
					throw new SerializationException("An error occured during serialization.", result.Exception);
				}

				if(callback != null)
				{
					callback(buffer.Data, result.Value);
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
		/// Deserialize an object from a file
		/// </summary>
		/// <typeparam name="T">The type of the object to deserialize.</typeparam>
		/// <param name="filename">The name of the file where to get the deserialized data.</param>
		/// <param name="callback">A callback to get the deserialized object.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <returns>An enumerator to wait for the deserialization completion.</returns>
		public IEnumerator Deserialize<T>(string filename, Action<T> callback, Action<float> progress = null)
		{
			byte[] data = System.IO.File.ReadAllBytes(filename);

			return Deserialize(data, callback, progress);
		}

		/// <summary>
		/// Deserialize an object from a byte array.
		/// </summary>
		/// <typeparam name="T">The type of the object to deserialize.</typeparam>
		/// <param name="data">The byte array containing the serialized data.</param>
		/// <param name="callback">A callback to get the deserialized object.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <returns>An enumerator to wait for the deserialization completion.</returns>
		public IEnumerator Deserialize<T>(byte[] data, Action<T> callback, Action<float> progress = null)
		{
			T value = default(T);

			float progress_percentage = 0f;

			using(Buffer buffer = GetBufferFromExistingData(data, p => progress_percentage = p))
			{
				SupportedTypes type = GetSupportedType(typeof(T));

				Threads.Result<uint> result = Threads.Tasks.Add(() => FromBytesWrapper(buffer, 0, out value, type));

				while(!result.Done)
				{
					if(progress != null)
					{
						progress(progress_percentage);
					}

					yield return null;
				}

				if(result.Exception != null)
				{
					throw new DeserializationException("An error occured during deserialization.", result.Exception);
				}
				else if(result.Value != data.Length)
				{
					throw new DeserializationException(string.Format("Invalid deserialization of type '{0}'. Not all available data was used.", typeof(T)), null);
				}
			}

			if(callback != null)
			{
				callback(value);
			}
		}
		#endregion

		#region Buffer handling
		/// <summary>
		/// Get a buffer of at least min_size.
		/// </summary>
		/// <remarks>The buffer can potentially be bigger, depending on the available allocated resources.</remarks>
		/// <param name="min_size">The minimal size of the buffer.</param>
		/// <param name="progress_callback">A callback to notify progress of the current task.</param>
		/// <returns>A buffer.</returns>
		public Buffer GetBuffer(uint min_size, Action<float> progress = null)
		{
			return new Buffer(this, min_size, progress);
		}

		/// <summary>
		/// Get a buffer from existing data.
		/// </summary>
		/// <param name="data">The existing data/</param>
		/// <param name="progress_callback">A callback to notify progress of the current task.</param>
		/// <returns>A buffer.</returns>
		public Buffer GetBufferFromExistingData(byte[] data, Action<float> progress = null)
		{
			return new Buffer(this, data, progress);
		}

		/// <summary>
		/// Resize a buffer to a new size of at least min_size.
		/// </summary>
		/// <remarks>The buffer can potentially be bigger, depending on the available allocated resources.</remarks>
		/// <param name="buffer">The buffer to resize.</param>
		/// <param name="min_size">The new minimal size of the buffer.</param>
		public void ResizeBuffer(ref Buffer buffer, uint min_size)
		{
			if(buffer == null)
			{
				throw new ArgumentNullException("buffer", "Can not resize undefined buffer.");
			}
			else if(buffer.Data.Length < min_size) // Buffer too small: resize
			{
				// Get how much memory we need. The idea is to reduce the need of further resizes down the road
				// for buffers that are frequently resized, while avoiding to get too much memory for buffers
				// of relatively constant size. Therefore, we allocate at least the size needed, plus an offset
				// that depends on the number of times this buffer has been resized, as well as the relative
				// impact of this resize (to avoid allocating huge amount of memory if a resize increase drastically
				// the size of the buffer. Hopefully, this algorithm should allow a fast convergence to the
				// ideal buffer size. However, keep in mind that resizes should be a last resort and should be avoided
				// when possible.
				uint current_size = (uint) buffer.Data.Length;
				float growth = Math.Max(1f - ((float) min_size) / current_size, minResizeOffset);
				uint new_size = min_size + (uint) (buffer.ResizeCount * growth * min_size);

				// Get a new buffer of sufficient size
				Buffer new_buffer = new Buffer(this, new_size, buffer.Progress, buffer.ResizeCount + 1);

				// Copy old buffer content into new one
				Array.Copy(buffer.Data, new_buffer.Data, buffer.Data.Length);

				// Release old buffer
				// Actually, do not call dispose for this buffer! If we do, it will be added back to the pool
				// of available buffers and the allocated memory could increase drastically over time.
				// Instead, we purposefully ignore to release it. Therefore, the memory will be released when
				// the buffer gets out of scope, i.e. at the end of this function.
				buffer.Dispose(false);

				// Switch buffers
				buffer = new_buffer;
			}
		}

		private byte[] Grab(uint min_size)
		{
			byte[] buffer = null;

			lock(available)
			{
				// Is their some available buffer ?
				if(available.Count > 0)
				{
					// Get the first buffer of sufficient size
					for(LinkedListNode<byte[]> it = available.First; it != null; it = it.Next)
					{
						if(it.Value.Length >= min_size)
						{
							buffer = it.Value;

							available.Remove(it);

							break;
						}
					}

					// No buffer wide enough ? Resize the smallest one to fit
					if(buffer == null)
					{
						// Avoid creating too many buffers that would ultimately pollute the pool
						available.RemoveFirst();

						// The actual buffer will be created later to avoid doing it in the lock scope
					}
				}
				// else no buffer available : create a new one. But we will do it later, out of the lock scope.
			}

			if(buffer == null)
			{
				// Buffer still null ? We need to create it of sufficient size. It may be that no buffer is available,
				// or that we are resizing the smallest one.
				buffer = new byte[min_size];
			}

			return buffer;
		}

		private void Release(byte[] buffer)
		{
			if(buffer != null)
			{
				lock(available)
				{
					int size = buffer.Length;

					// Store the buffer back in the sorted list of available buffers
					if(available.Count <= 0 || available.Last.Value.Length <= size)
					{
						// Either no buffer in list or buffer bigger than the bigger one : store it at the end
						available.AddLast(buffer);
					}
					else
					{
						// Add it before the first element of same size or bigger
						for(LinkedListNode<byte[]> it = available.First; it != null; it = it.Next)
						{
							if(it.Value.Length >= size)
							{
								available.AddBefore(it, buffer);

								break;
							}
						}
					}
				}
			}
		}
		#endregion

		#region Convert from bytes
		/// <summary>
		/// Deserialize a IBinarySerializable object.
		/// </summary>
		/// <typeparam name="T">The type of the IBinarySerializable object.</typeparam>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized object.</param>
		/// <param name="value">The deserialized object.</param>
		/// <param name="optional">If true, no error will be raised if the value is missing and null will be returned.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes<T>(Buffer buffer, uint start, out T value, bool optional = false) where T : IBinarySerializable
		{
			IBinarySerializable ret;
			
			uint read = FromBytes(buffer, start, out ret, typeof(T), optional);

			value = (T) ret;

			return read;
		}

		// Create a wrapper to call from recursive call where generic type constraints can not be matched
		private uint FromBytes(Buffer buffer, uint start, out IBinarySerializable value, System.Type type, bool optional = false)
		{
			uint read = 0;
			bool defined = true;

			CheckDeserializationParameters(buffer, start);

			if(optional)
			{
				read = FromBytes(buffer, start, out defined);
			}

			if(defined)
			{
				CallDefaultConstructor(type, out value);

				read += value.FromBytes(this, buffer, start + read);
			}
			else
			{
				value = null;
			}

			return read;
		}

		/// <summary>
		/// Deserialize a byte value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out byte value)
		{
			CheckDeserializationParameters(buffer, start);

			byte[] data = buffer.Data;

			if(start + byteSize > data.Length)
			{
				throw new ArgumentException(string.Format("Buffer too small. {0} bytes required, only {1} bytes available.", byteSize, data.Length - start));
			}

			value = data[start];

			if(buffer.Progress != null)
			{
				buffer.Progress(((float) (start + byteSize)) / buffer.Data.Length);
			}

			return byteSize;
		}

		/// <summary>
		/// Deserialize a bool value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out bool value)
		{
			byte ret;

			uint read = FromBytes(buffer, start, out ret);

			value = (ret != 0);

			return read;
		}

		/// <summary>
		/// Deserialize a int value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out int value)
		{
			int begin, end, iter;

			CheckDeserializationParameters(buffer, start);

			byte[] data = buffer.Data;

			if(start + intSize > data.Length)
			{
				throw new ArgumentException(string.Format("Buffer too small. {0} bytes required, only {1} bytes available.", intSize, data.Length - start));
			}

			// Get in big endian form
			if(isLittleEndian)
			{
				begin = (int) (start + intSize - 1);
				end = (int) (begin - intSize);
				iter = -1;
			}
			else
			{
				begin = (int) start;
				end = (int) (begin + intSize);
				iter = 1;
			}

			value = 0;

			for(int i = begin, offset = 0; i != end; i += iter, offset += (int) byteBits)
			{
				value |= data[i] << offset;
			}

			if(buffer.Progress != null)
			{
				buffer.Progress(((float) (start + intSize)) / buffer.Data.Length);
			}

			return intSize;
		}

		/// <summary>
		/// Deserialize a uint value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out uint value)
		{
			int ret;

			uint read = FromBytes(buffer, start, out ret);

			value = (uint) ret;

			return read;
		}

		/// <summary>
		/// Deserialize a long value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out long value)
		{
			int i1, i2;

			uint read = FromBytes(buffer, start, out i1);
			read += FromBytes(buffer, start + read, out i2);

			if(isLittleEndian)
			{
				value = new Converter(i2, i1).Long;
			}
			else
			{
				value = new Converter(i1, i2).Long;
			}

			return read;
		}

		/// <summary>
		/// Deserialize a ulong value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out ulong value)
		{
			long ret;

			uint read = FromBytes(buffer, start, out ret);

			value = (ulong) ret;

			return read;
		}

		/// <summary>
		/// Deserialize a float value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out float value)
		{
			int ret;

			uint read = FromBytes(buffer, start, out ret);

			value = new Converter(ret).Float1;

			return read;
		}

		/// <summary>
		/// Deserialize a double value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out double value)
		{
			int i1, i2;

			uint read = FromBytes(buffer, start, out i1);
			read += FromBytes(buffer, start + read, out i2);

			if(isLittleEndian)
			{
				value = new Converter(i2, i1).Double;
			}
			else
			{
				value = new Converter(i1, i2).Double;
			}

			return read;
		}

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

		/// <summary>
		/// Deserialize a string value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out string value)
		{
			uint size;

			value = null;

			CheckDeserializationParameters(buffer, start);

			uint read = FromBytes(buffer, start, out size);

			if(read != uintSize)
			{
				throw new FormatException(string.Format("The number of read bytes does not match the expected count. Read {0} bytes instead of {1}.", read, uintSize));
			}

			if(size > 0)
			{
				if(start + size > buffer.Data.Length)
				{
					throw new ArgumentException(string.Format("Buffer too small. {0} bytes required, only {1} bytes available.", size, buffer.Data.Length - start));
				}

				value = System.Text.Encoding.UTF8.GetString(buffer.Data, (int) (start + read), (int) size);

				read += size;
			}

			if(buffer.Progress != null)
			{
				buffer.Progress(((float) (start + read)) / buffer.Data.Length);
			}

			return read;
		}
		#endregion

		#region Convert to bytes
		/// <summary>
		/// Serialize a IBinarySerializable object.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The object to serialize.</param>
		/// <param name="optional">If true, no error will be raised if the value is null.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, IBinarySerializable value, bool optional = false)
		{
			uint written = 0;

			CheckSerializationParameters(buffer, start);

			if(optional)
			{
				written = ToBytes(ref buffer, start, value != null);
			}

			if(value != null)
			{
				written += value.ToBytes(this, ref buffer, start + written);
			}
			else if(! optional)
			{
				throw new ArgumentNullException("value", "Invalid undefined mandatory value in serialization.");
			}

			return written;
		}

		/// <summary>
		/// Serialize a byte value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, byte value)
		{
			CheckSerializationParameters(buffer, start);

			// Resize buffer if necessary
			ResizeBuffer(ref buffer, start + byteSize);

			buffer.Data[start] = value;

			if(buffer.Progress != null)
			{
				buffer.Progress(((float) (start + byteSize)) / buffer.Data.Length);
			}

			return byteSize;
		}

		/// <summary>
		/// Serialize a bool value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, bool value)
		{
			return ToBytes(ref buffer, start, value ? (byte) 0x1 : (byte) 0x0);
		}

		/// <summary>
		/// Serialize a int value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, int value)
		{
			int begin, end, iter;

			CheckSerializationParameters(buffer, start);

			// Resize buffer if necessary
			ResizeBuffer(ref buffer, start + intSize);

			byte[] data = buffer.Data;

			// Store in big endian form
			if(isLittleEndian)
			{
				begin = (int) (start + intSize - 1);
				end = (int) (begin - intSize);
				iter = -1;
			}
			else
			{
				begin = (int) start;
				end = (int) (begin + intSize);
				iter = 1;
			}

			for(int i = begin, offset = 0; i != end; i += iter, offset += (int) byteBits)
			{
				data[i] = (byte) ((value >> offset) & mask);
			}

			if(buffer.Progress != null)
			{
				buffer.Progress(((float) (start + intSize)) / buffer.Data.Length);
			}

			return intSize;
		}

		/// <summary>
		/// Serialize a uint value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, uint value)
		{
			return ToBytes(ref buffer, start, (int) value);
		}

		/// <summary>
		/// Serialize a long value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, long value)
		{
			int i1, i2;

			Converter c = new Converter(value);

			if(isLittleEndian)
			{
				i1 = c.Int2;
				i2 = c.Int1;
			}
			else
			{
				i1 = c.Int1;
				i2 = c.Int2;
			}

			uint written = ToBytes(ref buffer, start, i1);
			written += ToBytes(ref buffer, start + written, i2);

			return written;
		}

		/// <summary>
		/// Serialize a ulong value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, ulong value)
		{
			return ToBytes(ref buffer, start, (long) value);
		}

		/// <summary>
		/// Serialize a float value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, float value)
		{
			return ToBytes(ref buffer, start, new Converter(value).Int1);
		}

		/// <summary>
		/// Serialize a double value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, double value)
		{
			int i1, i2;

			Converter c = new Converter(value);

			if(isLittleEndian)
			{
				i1 = c.Int2;
				i2 = c.Int1;
			}
			else
			{
				i1 = c.Int1;
				i2 = c.Int2;
			}

			uint written = ToBytes(ref buffer, start, i1);
			written += ToBytes(ref buffer, start + written, i2);

			return written;
		}

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

		/// <summary>
		/// Serialize a string value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, string value)
		{
			uint written = 0;

			CheckSerializationParameters(buffer, start);

			if(!string.IsNullOrEmpty(value))
			{
				// Get number of required bytes
				uint size = (uint) System.Text.Encoding.UTF8.GetByteCount(value);

				// Make sure the buffer is large enough
				ResizeBuffer(ref buffer, start + uintSize + size);

				// Encode the string length first
				written = ToBytes(ref buffer, start, size);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}

				// Add the string bytes to the buffer (in-place)
				written += (uint) System.Text.Encoding.UTF8.GetBytes(value, 0, value.Length, buffer.Data, (int) (start + uintSize));
			}
			else
			{
				written = ToBytes(ref buffer, start, 0u);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}
			}

			if(buffer.Progress != null)
			{
				buffer.Progress(((float) (start + written)) / buffer.Data.Length);
			}

			return written;
		}
		#endregion

		#region Arrays
		/// <summary>
		/// Deserialize an array of supported objects.
		/// </summary>
		/// <typeparam name="T">The type of objects in the array.</typeparam>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized object.</param>
		/// <param name="array">The deserialized array.</param>
		/// <returns>The number of deserialized bytes.</returns>
		[MethodLocator(MethodLocatorAttribute.Type.FROM_BYTES, MethodLocatorAttribute.Parameter.ARRAY)]
		public uint FromBytes<T>(Buffer buffer, uint start, out T[] array)
		{
			uint array_size, read;

			array = null;

			CheckDeserializationParameters(buffer, start);

			// Read number of elements in array
			read = FromBytes(buffer, start, out array_size);

			if(read != uintSize)
			{
				throw new FormatException(string.Format("The number of read bytes does not match the expected count. Read {0} bytes instead of {1}.", read, uintSize));
			}

			if(array_size > 0)
			{
				T value;

				// Create the final destination array
				array = new T[array_size];

				// Get the correct type overload to use
				SupportedTypes type = GetSupportedType(typeof(T));

				// Read each element one after another
				for(uint i = 0; i < array_size; ++i)
				{
					read += FromBytesWrapper(buffer, start + read, out value, type);

					// Save the correctly type value in the output array
					array[i] = value;
				}
			}

			return read;
		}

		/// <summary>
		/// Deserialize an array of bytes.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized object.</param>
		/// <param name="array">The deserialized array.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out byte[] array)
		{
			uint array_size, read;

			array = null;

			CheckDeserializationParameters(buffer, start);

			// Read number of elements in array
			read = FromBytes(buffer, start, out array_size);

			if(read != uintSize)
			{
				throw new FormatException(string.Format("The number of read bytes does not match the expected count. Read {0} bytes instead of {1}.", read, uintSize));
			}

			if(array_size > 0)
			{
				uint array_bytes_size = array_size * byteSize;

				if(start + array_bytes_size > buffer.Data.Length)
				{
					throw new ArgumentException(string.Format("Buffer too small. {0} bytes required, only {1} bytes available.", array_bytes_size, buffer.Data.Length - start));
				}

				// Create the final destination array
				array = new byte[array_size];

				// Copy elements as fast as possible
				Array.Copy(buffer.Data, (int) (start + read), array, 0, (int) array_bytes_size);

				read += array_bytes_size;
			}

			if(buffer.Progress != null)
			{
				buffer.Progress(((float) (start + read)) / buffer.Data.Length);
			}

			return read;
		}

		/// <summary>
		/// Serialize an array of supported objects.
		/// </summary>
		/// <typeparam name="T">The type of objects in the array.</typeparam>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="array">The array to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		[MethodLocator(MethodLocatorAttribute.Type.TO_BYTES, MethodLocatorAttribute.Parameter.ARRAY)]
		public uint ToBytes<T>(ref Buffer buffer, uint start, T[] array)
		{
			uint written;

			CheckSerializationParameters(buffer, start);

			// If array is not defined, just write the length = 0 to the stream
			if(array == null)
			{
				written = ToBytes(ref buffer, start, 0u);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}
			}
			else
			{
				uint type_size;

				uint array_size = (uint) array.Length;

				// Get the correct type overload to use
				SupportedTypes type = GetSupportedType(typeof(T));

				if(sizes.TryGetValue(type, out type_size)) // If the type size is not defined, we will need to use on-the-fly buffer resizing, which is less effective.
				{
					// Check wether our buffer is large enough to get all data
					ResizeBuffer(ref buffer, start + uintSize + array_size * type_size);
				}

				// Write the length of the array in the buffer
				written = ToBytes(ref buffer, start, array_size);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}

				// Write all data in the buffer
				for(uint i = 0; i < array_size; ++i)
				{
					written += ToBytesWrapper(ref buffer, start + written, array[i], type);
				}
			}

			return written;
		}

		/// <summary>
		/// Serialize an array of bytes.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="array">The array to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, byte[] array)
		{
			uint written;

			CheckSerializationParameters(buffer, start);

			// If array is not defined, just write the length = 0 to the stream
			if(array == null)
			{
				written = ToBytes(ref buffer, start, 0u);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}
			}
			else
			{
				uint array_size = (uint) array.Length;

				uint array_bytes_size = array_size * byteSize;

				// Check wether our buffer is large enough to get all data
				ResizeBuffer(ref buffer, start + uintSize + array_bytes_size);

				// Write the length of the array in the buffer
				written = ToBytes(ref buffer, start, array_size);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}

				// Write all data in the buffer as fast as possible
				Array.Copy(array, 0, buffer.Data, (int) (start + written), (int) array_bytes_size);

				written += array_bytes_size;
			}

			if(buffer.Progress != null)
			{
				buffer.Progress(((float) (start + written)) / buffer.Data.Length);
			}

			return written;
		}
		#endregion

		#region Dictionaries
		/// <summary>
		/// Deserialize a dictionary of supported objects.
		/// </summary>
		/// <typeparam name="T">The type of keys in the dictionary.</typeparam>
		/// <typeparam name="U">The type of values in the dictionary.</typeparam>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized object.</param>
		/// <param name="dict">The deserialized dictionary.</param>
		/// <returns>The number of deserialized bytes.</returns>
		[MethodLocator(MethodLocatorAttribute.Type.FROM_BYTES, MethodLocatorAttribute.Parameter.DICTIONARY)]
		public uint FromBytes<T, U>(Buffer buffer, uint start, out Dictionary<T, U> dict)
		{
			uint nb_elements;

			CheckDeserializationParameters(buffer, start);

			uint read = FromBytes(buffer, start, out nb_elements);

			if(read != uintSize)
			{
				throw new FormatException(string.Format("The number of read bytes does not match the expected count. Read {0} bytes instead of {1}.", read, uintSize));
			}

			if(nb_elements > 0)
			{
				dict = new Dictionary<T, U>((int) nb_elements);

				// Get the correct type overloads to use
				SupportedTypes type_key = GetSupportedType(typeof(T));
				SupportedTypes type_value = GetSupportedType(typeof(U));

				T key;
				U value;

				for(uint i = 0; i < nb_elements; ++i)
				{
					read += FromBytesWrapper(buffer, start + read, out key, type_key);
					read += FromBytesWrapper(buffer, start + read, out value, type_value);

					dict.Add(key, value);
				}
			}
			else
			{
				dict = null;
			}

			return read;
		}

		/// <summary>
		/// Serialize a dictionary of supported objects.
		/// </summary>
		/// <typeparam name="T">The type of keys in the dictionary.</typeparam>
		/// <typeparam name="U">The type of values in the dictionary.</typeparam>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="dict">The dictionary to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		[MethodLocator(MethodLocatorAttribute.Type.TO_BYTES, MethodLocatorAttribute.Parameter.DICTIONARY)]
		public uint ToBytes<T, U>(ref Buffer buffer, uint start, Dictionary<T, U> dict)
		{
			uint written, type_size;

			CheckSerializationParameters(buffer, start);

			if(dict == null)
			{
				written = ToBytes(ref buffer, start, 0u);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}
			}
			else
			{
				uint size = uintSize;

				uint nb_elements = (uint) dict.Count;

				// Get the correct type overloads to use
				SupportedTypes type_key = GetSupportedType(typeof(T));
				SupportedTypes type_value = GetSupportedType(typeof(U));

				if(sizes.TryGetValue(type_key, out type_size)) // If the type size is not defined, we will need to use on-the-fly buffer resizing, which is less effective.
				{
					size += nb_elements * type_size;
				}

				if(sizes.TryGetValue(type_value, out type_size)) // If the type size is not defined, we will need to use on-the-fly buffer resizing, which is less effective.
				{
					size += nb_elements * type_size;
				}

				ResizeBuffer(ref buffer, start + size);

				written = ToBytes(ref buffer, start, nb_elements);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}

				foreach(KeyValuePair<T, U> pair in dict)
				{
					written += ToBytesWrapper(ref buffer, start + written, pair.Key, type_key);
					written += ToBytesWrapper(ref buffer, start + written, pair.Value, type_value);
				}
			}

			return written;
		}
		#endregion

		#region Object dynamic serialization
		/// <summary>
		/// Deserialize objects where type is not known at compilation time.
		/// </summary>
		/// <remarks>
		/// However, to avoid the penalty introduced by handling objects of unknown type, as well as keep useful compiler errors,
		/// the handling of this special case is not merged with the rest of the serialization methods. Instead, the user must
		/// EXPLICITELY ask for those methods, and they do not allow recursive serialization (i.e.arrays or dictionaries of 'objects').
		/// </remarks>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized object.</param>
		/// <param name="value">The deserialized object.</param>
		/// <param name="optional">If true, no error will be raised if the value is missing and null will be returned.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytesDynamic(Buffer buffer, uint start, out object value, bool optional = false)
		{
			uint read = 0;
			bool defined = true;

			CheckDeserializationParameters(buffer, start);

			if(optional)
			{
				read = FromBytes(buffer, start, out defined);
			}

			if(defined)
			{
				byte type;

				read += FromBytes(buffer, start + read, out type);

				read += FromBytesWrapper(buffer, start + read, out value, (SupportedTypes) type);
			}
			else
			{
				value = null;
			}

			return read;
		}

		/// <summary>
		/// Serialize objects where type is not known at compilation time.
		/// </summary>
		/// <remarks>
		/// However, to avoid the penalty introduced by handling objects of unknown type, as well as keep useful compiler errors,
		/// the handling of this special case is not merged with the rest of the serialization methods. Instead, the user must
		/// EXPLICITELY ask for those methods, and they do not allow recursive serialization (i.e.arrays or dictionaries of 'objects').
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The serialized object.</param>
		/// <param name="optional">If true, no error will be raised if the value is null.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytesDynamic(ref Buffer buffer, uint start, object value, bool optional = false)
		{
			uint written = 0;

			CheckSerializationParameters(buffer, start);

			if(optional)
			{
				written = ToBytes(ref buffer, start, value != null);
			}

			if(value != null)
			{
				SupportedTypes type = GetSupportedType(value.GetType());

				written += ToBytes(ref buffer, start + written, (byte) type);

				written += ToBytesWrapper(ref buffer, start + written, value, type);
			}
			else if(! optional)
			{
				throw new ArgumentNullException("value", "Invalid null mandatory value in serialization.");
			}

			return written;
		}
		#endregion

		#region Generics to type overloads casts
		protected void CallDefaultConstructor(System.Type type, out IBinarySerializable value)
		{
			ConstructorInfo constructor = type.GetConstructor(System.Type.EmptyTypes);

			if(constructor == null)
			{
				throw new ArgumentException(string.Format("Invalid deserialization of object of type '{0}'. No default constructor defined.", type.FullName));
			}

			value = (IBinarySerializable) constructor.Invoke(emptyParameters);
		}

		protected SupportedTypes GetSupportedType(System.Type type)
		{
			SupportedTypes result;

			if(type == typeof(byte))
			{
				result = SupportedTypes.BYTE;
			}
			else if(type == typeof(bool))
			{
				result = SupportedTypes.BOOL;
			}
			else if(type == typeof(int))
			{
				result = SupportedTypes.INT;
			}
			else if(type == typeof(uint))
			{
				result = SupportedTypes.UINT;
			}
			else if(type == typeof(long))
			{
				result = SupportedTypes.LONG;
			}
			else if(type == typeof(ulong))
			{
				result = SupportedTypes.ULONG;
			}
			else if(type == typeof(float))
			{
				result = SupportedTypes.FLOAT;
			}
			else if(type == typeof(double))
			{
				result = SupportedTypes.DOUBLE;
			}
			else if(type == typeof(string))
			{
				result = SupportedTypes.STRING;
			}
			else if(type == typeof(Vector2))
			{
				result = SupportedTypes.VECTOR2;
			}
			else if(type == typeof(Vector3))
			{
				result = SupportedTypes.VECTOR3;
			}
			else if(type == typeof(Vector4))
			{
				result = SupportedTypes.VECTOR4;
			}
			else if(type == typeof(Quaternion))
			{
				result = SupportedTypes.QUATERNION;
			}
			else if(type == typeof(Color))
			{
				result = SupportedTypes.COLOR;
			}
			else if(typeof(IBinarySerializable).IsAssignableFrom(type))
			{
				result = SupportedTypes.BINARY_SERIALIZABLE;
			}
			else if(type.IsArray)
			{
				result = SupportedTypes.ARRAY;
			}
#if UNITY_WSA && !UNITY_EDITOR
			else if(type.GetTypeInfo().IsGenericType && type.GetTypeInfo().GetGenericTypeDefinition() == typeof(Dictionary<,>))
#else
			else if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
#endif
			{
				result = SupportedTypes.DICTIONARY;
			}
			else
			{
				throw new ArgumentException(string.Format("The type '{0}' is not supported.", type));
			}

			return result;
		}

		protected uint FromBytesWrapper<T>(Buffer buffer, uint start, out T value, SupportedTypes type)
		{
			uint read;

			object[] parameters;

			switch(type)
			{
				case SupportedTypes.BINARY_SERIALIZABLE: // We cannot call the correct overload directly because the type constraints are not matched
					IBinarySerializable ret;

					// Optional parameter useless here: arrays and dictionnaries does call this wrapper only when strictly necessary,
					// i.e. when the value is defined. Moreover, the call of this wrapper from the Deserialize method also imply
					// that the value is mandatory. By forcing the optional parameter to false, we avoid allocating 1 more byte for
					// each value.
					read = FromBytes(buffer, start, out ret, typeof(T), false);

					value = (T) ret;

					break;
				case SupportedTypes.ARRAY:
					parameters = new object[nbParameters];

					parameters[0] = buffer;
					parameters[1] = start;
					parameters[2] = null;

					read = (uint) fromBytesArray.MakeGenericMethod(typeof(T).GetElementType()).Invoke(this, parameters);

					value = (T) parameters[2];

					break;
				case SupportedTypes.DICTIONARY:
					parameters = new object[nbParameters];

					parameters[0] = buffer;
					parameters[1] = start;
					parameters[2] = null;

					read = (uint) fromBytesDictionary.MakeGenericMethod(typeof(T).GetGenericArguments()).Invoke(this, parameters);

					value = (T) parameters[2];

					break;
				case SupportedTypes.BYTE:
					byte b;

					read = FromBytes(buffer, start, out b);

					value = (T) ((object) b);

					break;
				case SupportedTypes.BOOL:
					bool o;

					read = FromBytes(buffer, start, out o);

					value = (T) ((object) o);

					break;
				case SupportedTypes.INT:
					int i;

					read = FromBytes(buffer, start, out i);

					value = (T) ((object) i);

					break;
				case SupportedTypes.UINT:
					uint ui;

					read = FromBytes(buffer, start, out ui);

					value = (T) ((object) ui);

					break;
				case SupportedTypes.LONG:
					long l;

					read = FromBytes(buffer, start, out l);

					value = (T) ((object) l);

					break;
				case SupportedTypes.ULONG:
					ulong ul;

					read = FromBytes(buffer, start, out ul);

					value = (T) ((object) ul);

					break;
				case SupportedTypes.FLOAT:
					float f;

					read = FromBytes(buffer, start, out f);

					value = (T) ((object) f);

					break;
				case SupportedTypes.DOUBLE:
					double d;

					read = FromBytes(buffer, start, out d);

					value = (T) ((object) d);

					break;
				case SupportedTypes.STRING:
					string s;

					read = FromBytes(buffer, start, out s);

					value = (T) ((object) s);

					break;
				case SupportedTypes.VECTOR2:
					Vector2 v2;

					read = FromBytes(buffer, start, out v2);

					value = (T) ((object) v2);

					break;
				case SupportedTypes.VECTOR3:
					Vector3 v3;

					read = FromBytes(buffer, start, out v3);

					value = (T) ((object) v3);

					break;
				case SupportedTypes.VECTOR4:
					Vector4 v4;

					read = FromBytes(buffer, start, out v4);

					value = (T) ((object) v4);

					break;
				case SupportedTypes.QUATERNION:
					Quaternion q;

					read = FromBytes(buffer, start, out q);

					value = (T) ((object) q);

					break;
				case SupportedTypes.COLOR:
					Color c;

					read = FromBytes(buffer, start, out c);

					value = (T) ((object) c);

					break;
				default:
					throw new ArgumentException(string.Format("Unsupported Deserialization of type '{0}'.", typeof(T)));
			}

			return read;
		}

		protected uint ToBytesWrapper<T>(ref Buffer buffer, uint start, T value, SupportedTypes type)
		{
			uint written;

			object[] parameters;

			switch(type)
			{
				case SupportedTypes.BINARY_SERIALIZABLE:
					// Optional parameter useless here: arrays and dictionnaries does call this wrapper only when strictly necessary,
					// i.e. when the value is defined. Moreover, the call of this wrapper from the Serialize method also imply
					// that the value is mandatory. By forcing the optional parameter to false, we avoid allocating 1 more byte for
					// each value.
					written = ToBytes(ref buffer, start, (IBinarySerializable) ((object) value), false);
					break;
				case SupportedTypes.ARRAY:
					parameters = new object[nbParameters];

					parameters[0] = buffer;
					parameters[1] = start;
					parameters[2] = value;

					written = (uint) toBytesArray.MakeGenericMethod(typeof(T).GetElementType()).Invoke(this, parameters);

					buffer = (Buffer) parameters[0];

					break;
				case SupportedTypes.DICTIONARY:
					parameters = new object[nbParameters];

					parameters[0] = buffer;
					parameters[1] = start;
					parameters[2] = value;

					written = (uint) toBytesDictionary.MakeGenericMethod(typeof(T).GetGenericArguments()).Invoke(this, parameters);

					buffer = (Buffer) parameters[0];

					break;
				case SupportedTypes.BYTE:
					written = ToBytes(ref buffer, start, (byte) ((object) value));
					break;
				case SupportedTypes.BOOL:
					written = ToBytes(ref buffer, start, (bool) ((object) value));
					break;
				case SupportedTypes.INT:
					written = ToBytes(ref buffer, start, (int) ((object) value));
					break;
				case SupportedTypes.UINT:
					written = ToBytes(ref buffer, start, (uint) ((object) value));
					break;
				case SupportedTypes.LONG:
					written = ToBytes(ref buffer, start, (long) ((object) value));
					break;
				case SupportedTypes.ULONG:
					written = ToBytes(ref buffer, start, (ulong) ((object) value));
					break;
				case SupportedTypes.FLOAT:
					written = ToBytes(ref buffer, start, (float) ((object) value));
					break;
				case SupportedTypes.DOUBLE:
					written = ToBytes(ref buffer, start, (double) ((object) value));
					break;
				case SupportedTypes.STRING:
					written = ToBytes(ref buffer, start, (string) ((object) value));
					break;
				case SupportedTypes.VECTOR2:
					written = ToBytes(ref buffer, start, (Vector2) ((object) value));
					break;
				case SupportedTypes.VECTOR3:
					written = ToBytes(ref buffer, start, (Vector3) ((object) value));
					break;
				case SupportedTypes.VECTOR4:
					written = ToBytes(ref buffer, start, (Vector4) ((object) value));
					break;
				case SupportedTypes.QUATERNION:
					written = ToBytes(ref buffer, start, (Quaternion) ((object) value));
					break;
				case SupportedTypes.COLOR:
					written = ToBytes(ref buffer, start, (Color) ((object) value));
					break;
				default:
					throw new ArgumentException(string.Format("Unsupported serialization of type '{0}'.", typeof(T)));
			}

			return written;
		}
		#endregion

		#region Parameters checks
		protected void CheckSerializationParameters(Buffer buffer, uint start)
		{
			if(buffer == null)
			{
				throw new ArgumentNullException("buffer", "Invalid null buffer.");
			}

			if(start > buffer.Data.Length)
			{
				throw new ArgumentException(string.Format("Invalid start position '{0}' after end of buffer of size '{1}'", start, buffer.Data.Length));
			}
		}

		protected void CheckDeserializationParameters(Buffer buffer, uint start)
		{
			if(buffer == null)
			{
				throw new ArgumentNullException("Invalid null buffer.");
			}

			if(start >= buffer.Data.Length)
			{
				throw new ArgumentException(string.Format("Invalid start position '{0}' after end of buffer of size '{1}'", start, buffer.Data.Length));
			}
		}
		#endregion
	}
}
