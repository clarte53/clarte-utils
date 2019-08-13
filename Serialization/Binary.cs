using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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
		public enum SupportedTypes : byte
		{
            NONE = 0,
            BOOL,
            BYTE,
            SBYTE,
            CHAR,
            SHORT,
            USHORT,
			INT,
			UINT,
			LONG,
			ULONG,
			FLOAT,
			DOUBLE,
            DECIMAL,
            STRING,
            TYPE,
			VECTOR2,
			VECTOR3,
			VECTOR4,
			QUATERNION,
			COLOR,
            OBJECT,
            ENUM,
            ARRAY,
            LIST,
			DICTIONARY,
            BINARY_SERIALIZABLE,
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
        /// Helper class for compressed data saved as indexed values.
        /// </summary>
        /// <remarks>The actual values are automatically saved in the stream, interlaced with the other content.</remarks>
        /// <typeparam name="T">The type of values to save.</typeparam>
        public class IDMap<T>
        {
            #region Members
            protected Binary serializer;
            protected Serializer serializerCallback;
            protected Deserializer deserializerCallback;
            protected Dictionary<T, uint> ids;
            protected List<T> values;
            protected uint next;
            #endregion

            #region Delegates
            public delegate uint Serializer(Binary serializer, ref Buffer buffer, uint start, T value);
            public delegate uint Deserializer(Binary serializer, Buffer buffer, uint start, out T value);
            #endregion

            #region Constructors
            /// <summary>
            /// Create a new map.
            /// </summary>
            /// <param name="serializer">The serializer used by this mapping.</param>
            /// <param name="serializer_callback">Callback to serialize type T when required.</param>
            /// <param name="deserializer_callback">Callback to deserialize type T when required.</param>
            public IDMap(Binary serializer, Serializer serializer_callback, Deserializer deserializer_callback)
            {
                this.serializer = serializer;
                serializerCallback = serializer_callback;
                deserializerCallback = deserializer_callback;
                ids = new Dictionary<T, uint>();
                values = new List<T>();
                values.Add(default(T));
                next = 1;
            }
            #endregion

            #region Serialization methods
            /// <summary>
            /// Deserialize a compressed T value, stored in a mapping table.
            /// </summary>
            /// <param name="buffer">The buffer where to get the data from.</param>
            /// <param name="start">Start index of the data in the buffer.</param>
            /// <param name="value">The value to read in the buffer.</param>
            /// <returns></returns>
            public uint FromBytes(Buffer buffer, uint start, out T value)
            {
                uint read;
                uint id;

                if(next <= byte.MaxValue)
                {
                    byte b;

                    read = serializer.FromBytes(buffer, start, out b);

                    id = b;
                }
                else if(next <= ushort.MaxValue)
                {
                    ushort us;

                    read = serializer.FromBytes(buffer, start, out us);

                    id = us;
                }
                else
                {
                    read = serializer.FromBytes(buffer, start, out id);
                }

                if(id < values.Count)
                {
                    value = values[(int) id];
                }
                else if(id == values.Count)
                {
                    values.Add(default(T));
                    next = id + 1;

                    read += deserializerCallback(serializer, buffer, start + read, out value);

                    ids.Add(value, id);
                    values[(int) id] = value;
                }
                else
                {
                    throw new IndexOutOfRangeException(string.Format("Invalid index '{0}'. Some indexes are missing before this one.", id));
                }

                return read;
            }

            /// <summary>
            /// Serialize a T value in a compressed form, using a mapping table.
            /// </summary>
            /// <param name="buffer">The buffer where to store the data.</param>
            /// <param name="start">Start index where to store the data in the buffer.</param>
            /// <param name="value">The value to write in the buffer.</param>
            /// <returns></returns>
            public uint ToBytes(ref Buffer buffer, uint start, T value)
            {
                uint written;
                uint id;
                bool new_id = false;

                if(value == null)
                {
                    id = 0;
                }
                else if(! ids.TryGetValue(value, out id))
                {
                    id = next++;

                    if(id > int.MaxValue)
                    {
                        throw new IndexOutOfRangeException(string.Format("Index '{0}' in ID mapping is superior to the maximal supported index.", id));
                    }

                    ids.Add(value, id);
                    values.Add(value);

                    new_id = true;
                }

                int offset = (new_id ? 1 : 0);

                if(next <= byte.MaxValue - offset)
                {
                    written = serializer.ToBytes(ref buffer, start, (byte) id);
                }
                else if(next <= ushort.MaxValue - offset)
                {
                    written = serializer.ToBytes(ref buffer, start, (ushort) id);
                }
                else
                {
                    written = serializer.ToBytes(ref buffer, start, id);
                }

                if(new_id)
                {
                    written += serializerCallback(serializer, ref buffer, start + written, value);
                }

                return written;
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
            protected IDMap<Type> types;
			protected Action<float> progress;
			private bool disposed = false;
			#endregion

			#region Constructors
			/// <summary>
			/// Create a new buffer.
			/// </summary>
			/// <remarks>This is the shared constructors code. This constructor should never be called by itself.</remarks>
			/// <param name="manager">The associated serializer.</param>
			/// <param name="progress_callback">A callback to notify progress of the current task.</param>
            /// <param name="types_map">Optional existing map of types used by this buffer. DO NOT USE IT, except when resizing existing buffers.</param>
			protected Buffer(Binary manager, Action<float> progress_callback, IDMap<Type> types_map = null)
			{
				serializer = manager;
				progress = progress_callback;

                types = types_map ?? new IDMap<Type>(manager, ToBytes, FromBytes);
			}

            /// <summary>
            /// Create a new buffer of at least min_size bytes.
            /// </summary>
            /// <remarks>The buffer can potentially be bigger, depending on the available allocated resources.</remarks>
            /// <param name="manager">The associated serializer.</param>
            /// <param name="min_size">The minimal size of the buffer.</param>
            /// <param name="progress_callback">A callback to notify progress of the current task.</param>
            /// <param name="types_map">Optional existing map of types used by this buffer. DO NOT USE IT, except when resizing existing buffers.</param>
            /// <param name="resize_count">The number of times this buffer as been resized.</param>
            public Buffer(Binary manager, uint min_size, Action<float> progress_callback, IDMap<Type> types_map = null, uint resize_count = 0) : this(manager, progress_callback, types_map)
			{
				resizeCount = resize_count;

				data = serializer.Grab(min_size);
			}

            /// <summary>
            /// Create a new buffer from existing data.
            /// </summary>
            /// <param name="manager">The associated serializer.</param>
            /// <param name="existing_data">The existing data.</param>
            /// <param name="progress_callback">A callback to notify progress of the current task.</param>
            /// <param name="types_map">Optional existing map of types used by this buffer. DO NOT USE IT, except when resizing existing buffers.</param>
            public Buffer(Binary manager, byte[] existing_data, Action<float> progress_callback, IDMap<Type> types_map = null) : this(manager, progress_callback, types_map)
			{
				resizeCount = 0;

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
            /// Get the mapping table used for types serialization.
            /// </summary>
            public IDMap<Type> Types
            {
                get
                {
                    return types;
                }
            }

			/// <summary>
			/// Get the progress callback associated with this buffer.
			/// </summary>
			public Action<float> ProgressCallback
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
                    types = null;
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

			#region Progress
			/// <summary>
			/// Update the progress notification.
			/// </summary>
			/// <param name="position">Position reached in the buffer.</param>
			public void Progress(uint position)
			{
				if(progress != null)
				{
					progress(((float) position) / data.Length);
				}
			}
            #endregion

            #region Type serialization callbacks
            protected uint FromBytes(Binary serializer, Buffer buffer, uint start, out Type value)
            {
                string raw_complete_type;

                // Get the precise type of this object
                uint read = serializer.FromBytes(buffer, start, out raw_complete_type);

                // If an explicit type is defined, use it.
                if(! string.IsNullOrEmpty(raw_complete_type))
                {
                    try
                    {
                        value = Type.GetType(raw_complete_type);

                        if(value == null)
                        {
                            throw new TypeLoadException();
                        }
                    }
                    catch(Exception)
                    {
                        throw new SerializationException(string.Format("Missing type '{0}'. Use 'link.xml' files to include missing type in build.", raw_complete_type), new TypeLoadException(string.Format("Missing type '{0}'.", raw_complete_type)));
                    }
                }
                else
                {
                    value = null;
                }

                return read;
            }

            protected uint ToBytes(Binary serializer, ref Buffer buffer, uint start, Type value)
            {
                // Serialize the type info
                return serializer.ToBytes(ref buffer, start, value != null ? string.Format("{0}, {1}", value.ToString(), value.Assembly.GetName().Name) : "");
            }
            #endregion
        }

        #region Members
        /// <summary>
        /// Serialization buffer of 10 Mo by default.
        /// </summary>
        public const uint defaultSerializationBufferSize = 1024 * 1024 * 10;
		public const float minResizeOffset = 0.1f;

		protected const uint nbParameters = 3;
        protected const uint boolSize = sizeof(bool);
        protected const uint byteSize = sizeof(byte);
        protected const uint sbyteSize = sizeof(sbyte);
        protected const uint charSize = sizeof(char);
        protected const uint shortSize = sizeof(short);
        protected const uint ushortSize = sizeof(ushort);
        protected const uint intSize = sizeof(int);
		protected const uint uintSize = sizeof(uint);
		protected const uint longSize = sizeof(long);
		protected const uint ulongSize = sizeof(ulong);
		protected const uint floatSize = sizeof(float);
		protected const uint doubleSize = sizeof(double);
        protected const uint decimalSize = sizeof(decimal);

        private static readonly Dictionary<Type, SupportedTypes> mapping;
        private static readonly Dictionary<SupportedTypes, uint> sizes;
        private static readonly TimeSpan progressRefresRate;
		private static readonly object[] emptyParameters;

		private LinkedList<byte[]> available;
        #endregion

        #region Constructors
        static Binary()
        {
#pragma warning disable 0162
            if(boolSize != byteSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. ({2} != {3})", "byte", "bool", byteSize, boolSize));
            }

            if(sbyteSize != byteSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. ({2} != {3})", "byte", "sbyte", byteSize, sbyteSize));
            }

            if(charSize != 2 * byteSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (2 * {2} != {3})", "byte", "char", byteSize, charSize));
            }

            if(shortSize != 2 * byteSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (2 * {2} != {3})", "byte", "short", byteSize, shortSize));
            }

            if(ushortSize != shortSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. ({2} != {3})", "short", "ushort", shortSize, ushortSize));
            }

            if(intSize != 4 * byteSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (4 * {2} != {3})", "byte", "int", byteSize, intSize));
            }

            if(uintSize != intSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. ({2} != {3})", "int", "uint", intSize, uintSize));
            }

            if(longSize != 2 * intSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (2 * {2} != {3})", "int", "long", intSize, longSize));
            }

            if(ulongSize != longSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. ({2} != {3})", "long", "ulong", longSize, ulongSize));
            }

            if(floatSize != intSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. ({2} != {3})", "int", "float", intSize, floatSize));
            }

            if(doubleSize != 2 * floatSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (2 * {2} != {3})", "float", "double", floatSize, doubleSize));
            }

            if(decimalSize != 4 * floatSize)
            {
                throw new NotSupportedException(string.Format("The size of types '{0}' and '{1}' does not match. (4 * {2} != {3})", "float", "decimal", floatSize, decimalSize));
            }
#pragma warning restore 0162

            emptyParameters = new object[] { };

            progressRefresRate = new TimeSpan(0, 0, 0, 0, 40);

            mapping = new Dictionary<Type, SupportedTypes>()
			{
				{typeof(object), SupportedTypes.OBJECT},
				{typeof(bool), SupportedTypes.BOOL},
                {typeof(byte), SupportedTypes.BYTE},
                {typeof(sbyte), SupportedTypes.SBYTE},
                {typeof(char), SupportedTypes.CHAR},
                {typeof(short), SupportedTypes.SHORT},
                {typeof(ushort), SupportedTypes.USHORT},
                {typeof(int), SupportedTypes.INT},
                {typeof(uint), SupportedTypes.UINT},
                {typeof(long), SupportedTypes.LONG},
                {typeof(ulong), SupportedTypes.ULONG},
                {typeof(float), SupportedTypes.FLOAT},
                {typeof(double), SupportedTypes.DOUBLE},
                {typeof(decimal), SupportedTypes.DECIMAL},
                {typeof(string), SupportedTypes.STRING},
                {typeof(Type), SupportedTypes.TYPE},
                {typeof(Vector2), SupportedTypes.VECTOR2},
                {typeof(Vector3), SupportedTypes.VECTOR3},
                {typeof(Vector4), SupportedTypes.VECTOR4},
                {typeof(Quaternion), SupportedTypes.QUATERNION},
                {typeof(Color), SupportedTypes.COLOR}
            };

            sizes = new Dictionary<SupportedTypes, uint>()
            {
                {SupportedTypes.BOOL, boolSize},
                {SupportedTypes.BYTE, byteSize},
                {SupportedTypes.SBYTE, sbyteSize},
                {SupportedTypes.CHAR, charSize},
                {SupportedTypes.SHORT, shortSize},
                {SupportedTypes.USHORT, ushortSize},
                {SupportedTypes.INT, intSize},
                {SupportedTypes.UINT, uintSize},
                {SupportedTypes.LONG, longSize},
                {SupportedTypes.ULONG, ulongSize},
                {SupportedTypes.FLOAT, floatSize},
                {SupportedTypes.DOUBLE, doubleSize},
                {SupportedTypes.DECIMAL, decimalSize},
                {SupportedTypes.TYPE, intSize},
                {SupportedTypes.VECTOR2, 2 * floatSize},
                {SupportedTypes.VECTOR3, 3 * floatSize},
                {SupportedTypes.VECTOR4, 4 * floatSize},
                {SupportedTypes.QUATERNION, 4 * floatSize},
                {SupportedTypes.COLOR, 4 * byteSize}
            };
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
		/// Serialize an object to a byte array asynchronously.
		/// </summary>
		/// <param name="value">The value to serialize.</param>
		/// <param name="callback">A callback called once the data is serialized to get the result byte array and serialized size.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>An enumerator to wait for the serialization completion.</returns>
		public IEnumerator Serialize(object value, Action<byte[], uint> callback, Action<float> progress = null, uint default_buffer_size = defaultSerializationBufferSize)
		{
			Buffer buffer = null;

			try
			{
				DateTime time = DateTime.Now + progressRefresRate;
				float progress_percentage = 0f;

				buffer = GetBuffer(default_buffer_size, p => progress_percentage = p);

				Task<uint> result = Task.Run(() => ToBytes(ref buffer, 0, value));

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
		/// Serialize an object to a byte array synchronously.
		/// </summary>
		/// <param name="value">The value to serialize.</param>
		/// <param name="default_buffer_size">The default size to use for serialization buffer.</param>
		/// <returns>The serialized data.</returns>
		public byte[] Serialize(object value, uint default_buffer_size = defaultSerializationBufferSize)
		{
			byte[] result = null;

			Buffer buffer = null;
		
			try
			{
				buffer = GetBuffer(default_buffer_size);

				uint written = ToBytes(ref buffer, 0, value);

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
		/// Deserialize an object from a byte array asynchronously.
		/// </summary>
		/// <param name="data">The byte array containing the serialized data.</param>
		/// <param name="callback">A callback to get the deserialized object.</param>
		/// <param name="progress">A callback to get progress notifications.</param>
		/// <returns>An enumerator to wait for the deserialization completion.</returns>
		public IEnumerator Deserialize(byte[] data, Action<object> callback, Action<float> progress = null)
		{
			object value = null;

			DateTime time = DateTime.Now + progressRefresRate;
			float progress_percentage = 0f;

			using(Buffer buffer = GetBufferFromExistingData(data, p => progress_percentage = p))
			{
				Task<uint> result = Task.Run(() => FromBytes(buffer, 0, out value));

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

			if(callback != null)
			{
				callback(value);
			}
		}

		/// <summary>
		/// Deserialize an object from a byte array synchronously.
		/// </summary>
		/// <param name="data">The byte array containing the serialized data.</param>
		/// <returns>The deserialized object.</returns>
		public object Deserialize(byte[] data)
		{
			object value = null;

			using(Buffer buffer = GetBufferFromExistingData(data))
			{
				try
				{
					uint read = FromBytes(buffer, 0, out value);

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

			return value;
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
		/// <param name="data">The existing data.</param>
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
				Buffer new_buffer = new Buffer(this, new_size, buffer.ProgressCallback, buffer.Types, buffer.ResizeCount + 1);

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
		/// Deserialize a 16 bits value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		protected uint FromBytes(Buffer buffer, uint start, out Converter16 value)
        {
            byte b1, b2;

            uint read = FromBytes(buffer, start, out b1);
            read += FromBytes(buffer, start + read, out b2);

            value = new Converter16(b1, b2);

            return read;
        }

        /// <summary>
		/// Deserialize a 32 bits value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		protected uint FromBytes(Buffer buffer, uint start, out Converter32 value)
        {
            byte b1, b2, b3, b4;

            uint read = FromBytes(buffer, start, out b1);
            read += FromBytes(buffer, start + read, out b2);
            read += FromBytes(buffer, start + read, out b3);
            read += FromBytes(buffer, start + read, out b4);

            value = new Converter32(b1, b2, b3, b4);

            return read;
        }

        /// <summary>
		/// Deserialize a 64 bits value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		protected uint FromBytes(Buffer buffer, uint start, out Converter64 value)
        {
            Converter32 i1, i2;

            uint read = FromBytes(buffer, start, out i1);
            read += FromBytes(buffer, start + read, out i2);

            value = new Converter64(i1, i2);

            return read;
        }

        /// <summary>
		/// Deserialize a 128 bits value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		protected uint FromBytes(Buffer buffer, uint start, out Converter128 value)
        {
            Converter32 i1, i2, i3, i4;

            uint read = FromBytes(buffer, start, out i1);
            read += FromBytes(buffer, start + read, out i2);
            read += FromBytes(buffer, start + read, out i3);
            read += FromBytes(buffer, start + read, out i4);

            value = new Converter128(i1, i2, i3, i4);

            return read;
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

            buffer.Progress(start + byteSize);

            return byteSize;
        }

        /// <summary>
		/// Deserialize a sbyte value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out sbyte value)
        {
            byte b;

            uint read = FromBytes(buffer, start, out b);

            value = (sbyte) b;

            return read;
        }

        /// <summary>
		/// Deserialize a char value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out char value)
        {
            Converter16 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

            return read;
        }

        /// <summary>
		/// Deserialize a short value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out short value)
        {
            Converter16 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

            return read;
        }

        /// <summary>
		/// Deserialize a ushort value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out ushort value)
        {
            Converter16 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

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
            Converter32 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

            return read;
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
            Converter32 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

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
            Converter64 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

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
            Converter64 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

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
            Converter32 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

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
            Converter64 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

            return read;
        }

        /// <summary>
		/// Deserialize a decimal value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out decimal value)
        {
            Converter128 c;

            uint read = FromBytes(buffer, start, out c);

            value = c;

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

            buffer.Progress(start + read);

            return read;
        }

        /// <summary>
		/// Deserialize a Type value.
		/// </summary>
		/// <param name="buffer">The buffer containing the serialized data.</param>
		/// <param name="start">The start index in the buffer of the serialized value.</param>
		/// <param name="value">The deserialized value.</param>
		/// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out Type value)
        {
            return buffer.Types.FromBytes(buffer, start, out value);
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
		#endregion

		#region Convert to bytes
        /// <summary>
        /// Serialize a 16 bits value.
        /// </summary>
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, Converter16 value)
        {
            CheckSerializationParameters(buffer, start);

            // Resize buffer if necessary
            ResizeBuffer(ref buffer, start + shortSize);

            byte[] data = buffer.Data;

            data[start] = value.Byte1;
            data[start + 1] = value.Byte2;

            buffer.Progress(start + shortSize);

            return shortSize;
        }

        /// <summary>
        /// Serialize a 32 bits value.
        /// </summary>
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, Converter32 value)
        {
            CheckSerializationParameters(buffer, start);

            // Resize buffer if necessary
            ResizeBuffer(ref buffer, start + intSize);

            byte[] data = buffer.Data;

            data[start] = value.Byte1;
            data[start + 1] = value.Byte2;
            data[start + 2] = value.Byte3;
            data[start + 3] = value.Byte4;

            buffer.Progress(start + intSize);

            return intSize;
        }

        /// <summary>
        /// Serialize a 64 bits value.
        /// </summary>
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, Converter64 value)
        {
            CheckSerializationParameters(buffer, start);

            // Resize buffer if necessary
            ResizeBuffer(ref buffer, start + longSize);

            uint written = ToBytes(ref buffer, start, value.Int1);
            written += ToBytes(ref buffer, start + written, value.Int2);

            return written;
        }

        /// <summary>
        /// Serialize a 128 bits value.
        /// </summary>
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, Converter128 value)
        {
            CheckSerializationParameters(buffer, start);

            // Resize buffer if necessary
            ResizeBuffer(ref buffer, start + decimalSize);

            uint written = ToBytes(ref buffer, start, value.Int1);
            written += ToBytes(ref buffer, start + written, value.Int2);
            written += ToBytes(ref buffer, start + written, value.Int3);
            written += ToBytes(ref buffer, start + written, value.Int4);

            return written;
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

			buffer.Progress(start + byteSize);

			return byteSize;
		}

        /// <summary>
		/// Serialize a sbyte value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, sbyte value)
        {
            return ToBytes(ref buffer, start, (byte) value);
        }

        /// <summary>
        /// Serialize a char value.
        /// </summary>
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, char value)
        {
            return ToBytes(ref buffer, start, (Converter16) value);
        }

        /// <summary>
        /// Serialize a short value.
        /// </summary>
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, short value)
        {
            return ToBytes(ref buffer, start, (Converter16) value);
        }

        /// <summary>
        /// Serialize a ushort value.
        /// </summary>
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="value">The value to serialize.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, ushort value)
        {
            return ToBytes(ref buffer, start, (Converter16) value);
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
            return ToBytes(ref buffer, start, (Converter32) value);
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
            return ToBytes(ref buffer, start, (Converter32) value);
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
            return ToBytes(ref buffer, start, (Converter64) value);
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
            return ToBytes(ref buffer, start, (Converter64) value);
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
            return ToBytes(ref buffer, start, (Converter32) value);
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
            return ToBytes(ref buffer, start, (Converter64) value);
        }

        /// <summary>
		/// Serialize a decimal value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, decimal value)
        {
            return ToBytes(ref buffer, start, (Converter128) value);
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

            buffer.Progress(start + written);

            return written;
        }

        /// <summary>
		/// Serialize a Type value.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The value to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, Type value)
        {
            return buffer.Types.ToBytes(ref buffer, start, value);
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
        #endregion

        #region Object dynamic serialization
        /// <summary>
        /// Deserialize objects where type is not known at compilation time.
        /// </summary>
        /// <param name="buffer">The buffer containing the serialized data.</param>
        /// <param name="start">The start index in the buffer of the serialized object.</param>
        /// <param name="value">The deserialized object.</param>
        /// <returns>The number of deserialized bytes.</returns>
        public uint FromBytes(Buffer buffer, uint start, out object value)
        {
            byte type;

            CheckDeserializationParameters(buffer, start);

            uint read = FromBytes(buffer, start, out type);

            if((SupportedTypes) type != SupportedTypes.NONE)
            {
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
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="value">The serialized object.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, object value)
        {
            CheckSerializationParameters(buffer, start);

            SupportedTypes type = GetSupportedType(value.GetType());

			if(type == SupportedTypes.OBJECT)
			{
				throw new ArgumentException("Can not serialize object of unspecialized type.", "value");
			}

            uint written = ToBytes(ref buffer, start, (byte) type);

            if(type != SupportedTypes.NONE)
            {
                written += ToBytesWrapper(ref buffer, start + written, value, type);
            }

            return written;
        }
        #endregion

        #region Enums
        /// <summary>
        /// Deserialize an enum.
        /// </summary>
        /// <param name="buffer">The buffer containing the serialized data.</param>
        /// <param name="start">The start index in the buffer of the serialized object.</param>
        /// <param name="enumerate">The deserialized enum.</param>
        /// <returns>The number of deserialized bytes.</returns>
        public uint FromBytes(Buffer buffer, uint start, out Enum enumerate)
        {
            Type type;

            uint read = FromBytes(buffer, start, out type);

            TypeCode underlying_type = Type.GetTypeCode(Enum.GetUnderlyingType(type));

            switch(underlying_type)
            {
                case TypeCode.Byte:
                    byte b;

                    read += FromBytes(buffer, start + read, out b);

                    enumerate = (Enum) Enum.ToObject(type, b);

                    break;
                case TypeCode.SByte:
                    sbyte sb;

                    read += FromBytes(buffer, start + read, out sb);

                    enumerate = (Enum) Enum.ToObject(type, sb);

                    break;
                case TypeCode.Int16:
                    short s;

                    read += FromBytes(buffer, start + read, out s);

                    enumerate = (Enum) Enum.ToObject(type, s);

                    break;
                case TypeCode.UInt16:
                    ushort us;

                    read += FromBytes(buffer, start + read, out us);

                    enumerate = (Enum) Enum.ToObject(type, us);

                    break;
                case TypeCode.Int32:
                    int i;

                    read += FromBytes(buffer, start + read, out i);

                    enumerate = (Enum) Enum.ToObject(type, i);

                    break;
                case TypeCode.UInt32:
                    uint ui;

                    read += FromBytes(buffer, start + read, out ui);

                    enumerate = (Enum) Enum.ToObject(type, ui);

                    break;
                case TypeCode.Int64:
                    long l;

                    read += FromBytes(buffer, start + read, out l);

                    enumerate = (Enum) Enum.ToObject(type, l);

                    break;
                case TypeCode.UInt64:
                    ulong ul;

                    read += FromBytes(buffer, start + read, out ul);

                    enumerate = (Enum) Enum.ToObject(type, ul);

                    break;
                default:
                    throw new DeserializationException(string.Format("Unsupported enum underlying type. '{0}' is not a valid integral type for enums.", underlying_type), new TypeInitializationException(type.ToString(), null));
            }

            return read;
        }

        /// <summary>
		/// Serialize an enum.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="enumerate">The enum to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, Enum enumerate)
        {
            TypeCode type = enumerate.GetTypeCode();

            uint written = ToBytes(ref buffer, start, enumerate.GetType());

            switch(type)
            {
                case TypeCode.Byte:
                    written += ToBytes(ref buffer, start + written, (byte) ((object) enumerate));
                    break;
                case TypeCode.SByte:
                    written += ToBytes(ref buffer, start + written, (sbyte) ((object) enumerate));
                    break;
                case TypeCode.Int16:
                    written += ToBytes(ref buffer, start + written, (short) ((object) enumerate));
                    break;
                case TypeCode.UInt16:
                    written += ToBytes(ref buffer, start + written, (ushort) ((object) enumerate));
                    break;
                case TypeCode.Int32:
                    written += ToBytes(ref buffer, start + written, (int) ((object) enumerate));
                    break;
                case TypeCode.UInt32:
                    written += ToBytes(ref buffer, start + written, (uint) ((object) enumerate));
                    break;
                case TypeCode.Int64:
                    written += ToBytes(ref buffer, start + written, (long) ((object) enumerate));
                    break;
                case TypeCode.UInt64:
                    written += ToBytes(ref buffer, start + written, (ulong) ((object) enumerate));
                    break;
                default:
                    throw new SerializationException(string.Format("Unsupported enum underlying type. '{0}' is not a valid integral type for enums.", type), new TypeInitializationException(typeof(Enum).ToString(), null));
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
		public uint FromBytes<T>(Buffer buffer, uint start, out T[] array)
		{
            Array a;

            uint read = FromBytes(buffer, start, out a);

            array = (T[]) a;

            return read;
		}

        public uint FromBytes(Buffer buffer, uint start, out Array array)
        {
            uint array_size, read;
            Type element_type;

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
                object value;

                // Read the type of the array's elements
                read += FromBytes(buffer, start + read, out element_type);

                if(element_type == null)
                {
                    throw new FormatException("The type of elements in the array is not defined.");
                }

                // Create the final destination array
                array = Array.CreateInstance(element_type, array_size);

                // Get the correct type overload to use
                SupportedTypes type = GetSupportedType(element_type);

                // Read each element one after another
                for(uint i = 0; i < array_size; ++i)
                {
                    read += FromBytesWrapper(buffer, start + read, out value, type);

                    // Save the correctly type value in the output array
                    array.SetValue(value, i);
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
            Type element_type;

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

                // Read the type of the array's elements
                read += FromBytes(buffer, start + read, out element_type);

                if(element_type == null)
                {
                    throw new FormatException("The type of elements in the array is not defined.");
                }

                if(element_type != typeof(byte))
                {
                    throw new FormatException("The type of elements in the array is not 'byte' as expected.");
                }

                // Create the final destination array
                array = new byte[array_size];

				// Copy elements as fast as possible
				Array.Copy(buffer.Data, (int) (start + read), array, 0, (int) array_bytes_size);

				read += array_bytes_size;
			}

			buffer.Progress(start + read);

			return read;
		}

		/// <summary>
		/// Serialize an array of supported objects.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="array">The array to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, Array array)
		{
			uint written;

			CheckSerializationParameters(buffer, start);

			// If array is not defined, just write the length = 0 to the stream
			if(array == null || array.Length <= 0)
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
                Type element_type = array.GetType().GetElementType();
                SupportedTypes type = GetSupportedType(element_type);

                if(type == SupportedTypes.NONE)
                {
                    throw new ArgumentException(string.Format("Unsupported array type '{0}'. Values type is unsupported.", array.GetType()), "array");
                }

				// Write the length of the array in the buffer
				written = ToBytes(ref buffer, start, array_size);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}

                // Write the type of the array's elements
                written += ToBytes(ref buffer, start + written, element_type);

				if(sizes.TryGetValue(type, out type_size)) // If the type size is not defined, we will need to use on-the-fly buffer resizing, which is less effective.
				{
					// Check wether our buffer is large enough to get all data
					ResizeBuffer(ref buffer, start + written + array_size * type_size);
				}

				// Write all data in the buffer
				for(uint i = 0; i < array_size; ++i)
				{
					written += ToBytesWrapper(ref buffer, start + written, array.GetValue(i), type);
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
			if(array == null || array.Length <= 0)
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

				// Write the length of the array in the buffer
				written = ToBytes(ref buffer, start, array_size);

				if(written != uintSize)
				{
					throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
				}

                // Write the type of the array's elements
                written += ToBytes(ref buffer, start + written, typeof(byte));

				// Check wether our buffer is large enough to get all data
				ResizeBuffer(ref buffer, start + written + array_bytes_size);

				// Write all data in the buffer as fast as possible
				Array.Copy(array, 0, buffer.Data, (int) (start + written), (int) array_bytes_size);

				written += array_bytes_size;
			}

			buffer.Progress(start + written);

			return written;
		}
        #endregion

        #region Lists
        /// <summary>
        /// Deserialize a list of supported objects.
        /// </summary>
        /// <typeparam name="T">The type of objects in the list.</typeparam>
        /// <param name="buffer">The buffer containing the serialized data.</param>
        /// <param name="start">The start index in the buffer of the serialized object.</param>
        /// <param name="list">The deserialized list.</param>
        /// <returns>The number of deserialized bytes.</returns>
        public uint FromBytes<T>(Buffer buffer, uint start, out List<T> list)
        {
            IList l;

            uint read = FromBytes(buffer, start, out l);

            list = (List<T>) l;

            return read;
        }

        /// <summary>
        /// Deserialize a list of supported objects.
        /// </summary>
        /// <param name="buffer">The buffer containing the serialized data.</param>
        /// <param name="start">The start index in the buffer of the serialized object.</param>
        /// <param name="list">The deserialized list.</param>
        /// <returns>The number of deserialized bytes.</returns>
        public uint FromBytes(Buffer buffer, uint start, out IList list)
        {
            uint list_size, read;
            Type element_type;

            list = null;

            CheckDeserializationParameters(buffer, start);

            // Read number of elements in list
            read = FromBytes(buffer, start, out list_size);

            if(read != uintSize)
            {
                throw new FormatException(string.Format("The number of read bytes does not match the expected count. Read {0} bytes instead of {1}.", read, uintSize));
            }

            if(list_size > 0)
            {
                object value;

                // Read the type of the list's elements
                read += FromBytes(buffer, start + read, out element_type);

                if(element_type == null)
                {
                    throw new FormatException("The type of elements in the list is not defined.");
                }

                // Create the final destination list (with correct capacity)
                list = (IList) Activator.CreateInstance(typeof(List<>).MakeGenericType(element_type), (int) list_size);

                // Get the correct type overload to use
                SupportedTypes type = GetSupportedType(element_type);

                // Read each element one after another
                for(uint i = 0; i < list_size; ++i)
                {
                    read += FromBytesWrapper(buffer, start + read, out value, type);

                    // Save the correctly type value in the output list
                    list.Add(value);
                }
            }

            return read;
        }

        /// <summary>
        /// Serialize a list of supported objects.
        /// </summary>
        /// <param name="buffer">The buffer where to serialize the data.</param>
        /// <param name="start">The start index in the buffer where to serialize the data.</param>
        /// <param name="list">The list to serialize.</param>
        /// <returns>The number of serialized bytes.</returns>
        public uint ToBytes(ref Buffer buffer, uint start, IList list)
        {
            uint written;

            CheckSerializationParameters(buffer, start);

            // If list is not defined, just write the length = 0 to the stream
            if(list == null || list.Count <= 0)
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

                uint list_size = (uint) list.Count;

                Type[] params_types = GetGenericParametersTypes(list.GetType());
                Type element_type = (params_types != null && params_types.Length >= 1 ? params_types[0] : null);

                // Get the correct type overload to use
                SupportedTypes type = GetSupportedType(element_type);

                if(type == SupportedTypes.NONE)
                {
                    throw new ArgumentException(string.Format("Unsupported list type '{0}'. Either list type or values type are unsupported.", list.GetType()), "list");
                } 

                // Write the length of the list in the buffer
                written = ToBytes(ref buffer, start, list_size);

                if(written != uintSize)
                {
                    throw new FormatException(string.Format("The number of written bytes does not match the expected count. Wrote {0} bytes instead of {1}.", written, uintSize));
                }

                // Write the type of the list's elements
                written += ToBytes(ref buffer, start + written, element_type);

				if(sizes.TryGetValue(type, out type_size)) // If the type size is not defined, we will need to use on-the-fly buffer resizing, which is less effective.
				{
					// Check wether our buffer is large enough to get all data
					ResizeBuffer(ref buffer, start + written + list_size * type_size);
				}

				// Write all data in the buffer
				for(uint i = 0; i < list_size; ++i)
                {
                    written += ToBytesWrapper(ref buffer, start + written, list[(int) i], type);
                }
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
        public uint FromBytes<T, U>(Buffer buffer, uint start, out Dictionary<T, U> dict)
        {
            IDictionary d;

            uint read = FromBytes(buffer, start, out d);

            dict = (Dictionary<T, U>) d;

            return read;
        }

        /// <summary>
        /// Deserialize a dictionary of supported objects.
        /// </summary>
        /// <param name="buffer">The buffer containing the serialized data.</param>
        /// <param name="start">The start index in the buffer of the serialized object.</param>
        /// <param name="dict">The deserialized dictionary.</param>
        /// <returns>The number of deserialized bytes.</returns>
		public uint FromBytes(Buffer buffer, uint start, out IDictionary dict)
		{
			uint nb_elements;
            Type key_element_type, value_element_type;

            CheckDeserializationParameters(buffer, start);

			uint read = FromBytes(buffer, start, out nb_elements);

			if(read != uintSize)
			{
				throw new FormatException(string.Format("The number of read bytes does not match the expected count. Read {0} bytes instead of {1}.", read, uintSize));
			}

			if(nb_elements > 0)
			{
                // Read the type of the dictionary's keys
                read += FromBytes(buffer, start + read, out key_element_type);

                if(key_element_type == null)
                {
                    throw new FormatException("The type of keys in the dictionary is not defined.");
                }

                // Read the type of the dictionary's values
                read += FromBytes(buffer, start + read, out value_element_type);

                if(value_element_type == null)
                {
                    throw new FormatException("The type of values in the dictionary is not defined.");
                }

                dict = (IDictionary) Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(key_element_type, value_element_type), (int) nb_elements);

                // Get the correct type overloads to use
                SupportedTypes type_key = GetSupportedType(key_element_type);
				SupportedTypes type_value = GetSupportedType(value_element_type);

				object key;
                object value;

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
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="dict">The dictionary to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, IDictionary dict)
		{
			uint written, type_size;

			CheckSerializationParameters(buffer, start);

			if(dict == null || dict.Count <= 0)
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

                Type[] params_types = GetGenericParametersTypes(dict.GetType());
                Type key_element_type = (params_types != null && params_types.Length >= 1 ? params_types[0] : null);
                Type value_element_type = (params_types != null && params_types.Length >= 2 ? params_types[1] : null);

                // Get the correct type overloads to use
                SupportedTypes type_key = GetSupportedType(key_element_type);
                SupportedTypes type_value = GetSupportedType(value_element_type);

                if(type_key == SupportedTypes.NONE | type_value == SupportedTypes.NONE)
                {
                    throw new ArgumentException(string.Format("Unsupported dictionary type '{0}'. Either dictionnary type or keys / values types are unsupported.", dict.GetType()), "dict");
                }

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

                // Write the type of the dictionary's keys and values
                written += ToBytes(ref buffer, start + written, key_element_type);
                written += ToBytes(ref buffer, start + written, value_element_type);

                foreach(DictionaryEntry pair in dict)
				{
					written += ToBytesWrapper(ref buffer, start + written, pair.Key, type_key);
					written += ToBytesWrapper(ref buffer, start + written, pair.Value, type_value);
				}
			}

			return written;
		}
        #endregion

        #region IBinarySerializable serialization
        /// <summary>
        /// Deserialize a IBinarySerializable object.
        /// </summary>
        /// <param name="buffer">The buffer containing the serialized data.</param>
        /// <param name="start">The start index in the buffer of the serialized object.</param>
        /// <param name="value">The deserialized object.</param>
        /// <returns>The number of deserialized bytes.</returns>
        public uint FromBytes(Buffer buffer, uint start, out IBinarySerializable value)
        {
            Type type;

            CheckDeserializationParameters(buffer, start);

            uint read = FromBytes(buffer, start, out type);

            if(type != null)
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
        /// Deserialize into an existing IBinarySerializable object.
        /// </summary>
        /// <param name="buffer">The buffer containing the serialized data.</param>
        /// <param name="start">The start index in the buffer of the serialized object.</param>
        /// <param name="value">The deserialized object.</param>
        /// <returns>The number of deserialized bytes.</returns>
        public uint FromBytesOverwrite(Buffer buffer, uint start, IBinarySerializable value)
        {
            Type type;

            CheckDeserializationParameters(buffer, start);

            uint read = FromBytes(buffer, start, out type);

            if(type != null)
            {
                if(!type.IsAssignableFrom(value.GetType()))
                {
                    throw new FormatException("The type of IBynarySerializable object does not match the provided object.");
                }

                read += value.FromBytes(this, buffer, start + read);
            }
            else
            {
                // Nothing to do: we got nothing to deserialize and the value already exists, so returning null is not an option.
                // It would be great to notify the user, but we do have a mecanism for that, and raising an exception would stop
                // the deserialization, but this should just be a warning.
            }

            return read;
        }

        /// <summary>
		/// Serialize a IBinarySerializable object.
		/// </summary>
		/// <param name="buffer">The buffer where to serialize the data.</param>
		/// <param name="start">The start index in the buffer where to serialize the data.</param>
		/// <param name="value">The object to serialize.</param>
		/// <returns>The number of serialized bytes.</returns>
		public uint ToBytes(ref Buffer buffer, uint start, IBinarySerializable value)
        {
            uint written;

            CheckSerializationParameters(buffer, start);

            if(value != null)
            {
                Type type = value.GetType();

                CheckDefaultConstructor(type);

                written = ToBytes(ref buffer, start, type);

                written += value.ToBytes(this, ref buffer, start + written);
            }
            else
            {
                written = ToBytes(ref buffer, start, (Type) null);
            }

            return written;
        }
        #endregion

        #region Generics to type overloads casts
        protected static ConstructorInfo CheckDefaultConstructor(Type type)
        {
            ConstructorInfo constructor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);

            if(constructor == null)
            {
                throw new ArgumentException(string.Format("Invalid deserialization of object of type '{0}'. No default constructor defined.", type.FullName));
            }

            return constructor;
        }

        protected static void CallDefaultConstructor(Type type, out IBinarySerializable value)
		{
			value = (IBinarySerializable) CheckDefaultConstructor(type).Invoke(emptyParameters);
		}

		public static SupportedTypes GetSupportedType(Type type)
		{
			SupportedTypes result;

            if(!mapping.TryGetValue(type, out result))
            {
                if(typeof(IBinarySerializable).IsAssignableFrom(type))
                {
                    result = SupportedTypes.BINARY_SERIALIZABLE;
                }
                else if(type.IsEnum)
                {
                    result = SupportedTypes.ENUM;
                }
                else if(type.IsArray)
                {
                    result = SupportedTypes.ARRAY;

                    // Check inner type support
                    GetSupportedType(type.GetElementType());
                }
                else if(CheckGenericDefinition(type, typeof(List<>)))
                {
                    result = SupportedTypes.LIST;

                    // Check inner type support
                    CheckGenericParametersTypes(type, 1);
                }
                else if(CheckGenericDefinition(type, typeof(Dictionary<,>)))
                {
                    result = SupportedTypes.DICTIONARY;

                    // Check inner type support
                    CheckGenericParametersTypes(type, 2);
                }
                else
                {
                    throw new ArgumentException(string.Format("The type '{0}' is not supported.", type));
                }
            }

			return result;
		}

		protected uint FromBytesWrapper(Buffer buffer, uint start, out object value, SupportedTypes type)
		{
			uint read;

            switch(type)
			{
                case SupportedTypes.BOOL:
                    bool o;

                    read = FromBytes(buffer, start, out o);

                    value = o;

                    break;
                case SupportedTypes.BYTE:
					byte b;

					read = FromBytes(buffer, start, out b);

					value = b;

					break;
                case SupportedTypes.SBYTE:
                    sbyte sb;

                    read = FromBytes(buffer, start, out sb);

                    value = sb;

                    break;
                case SupportedTypes.CHAR:
                    char c;

                    read = FromBytes(buffer, start, out c);

                    value = c;

                    break;
                case SupportedTypes.SHORT:
                    short s;

                    read = FromBytes(buffer, start, out s);

                    value = s;

                    break;
                case SupportedTypes.USHORT:
                    ushort us;

                    read = FromBytes(buffer, start, out us);

                    value = us;

                    break;
                case SupportedTypes.INT:
					int i;

					read = FromBytes(buffer, start, out i);

					value = i;

					break;
				case SupportedTypes.UINT:
					uint ui;

					read = FromBytes(buffer, start, out ui);

					value = ui;

					break;
				case SupportedTypes.LONG:
					long l;

					read = FromBytes(buffer, start, out l);

					value = l;

					break;
				case SupportedTypes.ULONG:
					ulong ul;

					read = FromBytes(buffer, start, out ul);

					value = ul;

					break;
				case SupportedTypes.FLOAT:
					float f;

					read = FromBytes(buffer, start, out f);

					value = f;

					break;
				case SupportedTypes.DOUBLE:
					double d;

					read = FromBytes(buffer, start, out d);

					value = d;

					break;
                case SupportedTypes.DECIMAL:
                    decimal dec;

                    read = FromBytes(buffer, start, out dec);

                    value = dec;

                    break;
                case SupportedTypes.STRING:
					string str;

					read = FromBytes(buffer, start, out str);

					value = str;

					break;
                case SupportedTypes.TYPE:
                    Type t;

                    read = FromBytes(buffer, start, out t);

                    value = t;

                    break;
                case SupportedTypes.VECTOR2:
					Vector2 v2;

					read = FromBytes(buffer, start, out v2);

					value = v2;

					break;
				case SupportedTypes.VECTOR3:
					Vector3 v3;

					read = FromBytes(buffer, start, out v3);

					value = v3;

					break;
				case SupportedTypes.VECTOR4:
					Vector4 v4;

					read = FromBytes(buffer, start, out v4);

					value = v4;

					break;
				case SupportedTypes.QUATERNION:
					Quaternion q;

					read = FromBytes(buffer, start, out q);

					value = q;

					break;
				case SupportedTypes.COLOR:
					Color col;

					read = FromBytes(buffer, start, out col);

					value = col;

					break;
				case SupportedTypes.OBJECT:
					read = FromBytes(buffer, start, out value);

					break;
				case SupportedTypes.ENUM:
                    Enum e;

                    read = FromBytes(buffer, start, out e);

                    value = e;

                    break;
                case SupportedTypes.ARRAY:
                    Array a;

                    read = FromBytes(buffer, start, out a);

                    value = a;

                    break;
                case SupportedTypes.LIST:
                    IList li;

                    read = FromBytes(buffer, start, out li);

                    value = li;

                    break;
                case SupportedTypes.DICTIONARY:
                    IDictionary dic;

                    read = FromBytes(buffer, start, out dic);

                    value = dic;

                    break;
				case SupportedTypes.BINARY_SERIALIZABLE:
					IBinarySerializable ibs;

					read = FromBytes(buffer, start, out ibs);

					value = ibs;

					break;
				default:
					throw new ArgumentException(string.Format("Unsupported Deserialization of type '{0}'.", type));
			}

			return read;
		}

		protected uint ToBytesWrapper(ref Buffer buffer, uint start, object value, SupportedTypes type)
		{
			uint written;

            switch(type)
			{
                case SupportedTypes.BOOL:
                    written = ToBytes(ref buffer, start, (bool) value);
                    break;
                case SupportedTypes.BYTE:
					written = ToBytes(ref buffer, start, (byte) value);
					break;
                case SupportedTypes.SBYTE:
                    written = ToBytes(ref buffer, start, (sbyte) value);
                    break;
                case SupportedTypes.CHAR:
                    written = ToBytes(ref buffer, start, (char) value);
                    break;
                case SupportedTypes.SHORT:
                    written = ToBytes(ref buffer, start, (short) value);
                    break;
                case SupportedTypes.USHORT:
                    written = ToBytes(ref buffer, start, (ushort) value);
                    break;
                case SupportedTypes.INT:
					written = ToBytes(ref buffer, start, (int) value);
					break;
				case SupportedTypes.UINT:
					written = ToBytes(ref buffer, start, (uint) value);
					break;
				case SupportedTypes.LONG:
					written = ToBytes(ref buffer, start, (long) value);
					break;
				case SupportedTypes.ULONG:
					written = ToBytes(ref buffer, start, (ulong) value);
					break;
				case SupportedTypes.FLOAT:
					written = ToBytes(ref buffer, start, (float) value);
					break;
				case SupportedTypes.DOUBLE:
					written = ToBytes(ref buffer, start, (double) value);
					break;
                case SupportedTypes.DECIMAL:
                    written = ToBytes(ref buffer, start, (decimal) value);
                    break;
                case SupportedTypes.STRING:
					written = ToBytes(ref buffer, start, (string) value);
					break;
                case SupportedTypes.TYPE:
                    written = ToBytes(ref buffer, start, (Type) value);
                    break;
                case SupportedTypes.VECTOR2:
					written = ToBytes(ref buffer, start, (Vector2) value);
					break;
				case SupportedTypes.VECTOR3:
					written = ToBytes(ref buffer, start, (Vector3) value);
					break;
				case SupportedTypes.VECTOR4:
					written = ToBytes(ref buffer, start, (Vector4) value);
					break;
				case SupportedTypes.QUATERNION:
					written = ToBytes(ref buffer, start, (Quaternion) value);
					break;
				case SupportedTypes.COLOR:
					written = ToBytes(ref buffer, start, (Color) value);
					break;
				case SupportedTypes.OBJECT:
					written = ToBytes(ref buffer, start, value);
					break;
				case SupportedTypes.ENUM:
                    written = ToBytes(ref buffer, start, (Enum) value);
                    break;
                case SupportedTypes.ARRAY:
                    written = ToBytes(ref buffer, start, (Array) value);
                    break;
                case SupportedTypes.LIST:
                    written = ToBytes(ref buffer, start, (IList) value);
                    break;
                case SupportedTypes.DICTIONARY:
                    written = ToBytes(ref buffer, start, (IDictionary) value);
                    break;
				case SupportedTypes.BINARY_SERIALIZABLE:
					written = ToBytes(ref buffer, start, (IBinarySerializable) value);
					break;
				default:
					throw new ArgumentException(string.Format("Unsupported serialization of type '{0}' with object of type '{1}'.", type, value.GetType()));
			}

			return written;
		}
        #endregion

        #region Parameters checks
        protected static bool CheckGenericDefinition(Type type, Type generic_definition)
        {
#if NETFX_CORE
			return type.GetTypeInfo().IsGenericType && type.GetTypeInfo().GetGenericTypeDefinition() == generic_definition;
#else
            return type.IsGenericType && type.GetGenericTypeDefinition() == generic_definition;
#endif
        }

        protected static Type[] GetGenericParametersTypes(Type type)
        {
#if NETFX_CORE
            Type[] element_types = (type.GetTypeInfo().IsGenericType ? type.GetTypeInfo().GetGenericArguments() : null);
#else
            Type[] element_types = (type.IsGenericType ? type.GetGenericArguments() : null);
#endif

            return element_types;
        }

        protected static void CheckGenericParametersTypes(Type type, uint nb_expected_types)
        {
            Type[] element_types = GetGenericParametersTypes(type);

            if(element_types == null || element_types.Length < nb_expected_types)
            {
                throw new ArgumentException(string.Format("The type '{0}' is not supported.", type));
            }

            for(uint i = 0; i < nb_expected_types; i++)
            {
                // Check if we get an exception or not
                GetSupportedType(element_types[i]);
            }
        }

        protected static void CheckSerializationParameters(Buffer buffer, uint start)
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

		protected static void CheckDeserializationParameters(Buffer buffer, uint start)
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
