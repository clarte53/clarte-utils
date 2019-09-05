using System;
using System.Collections.Generic;

namespace CLARTE.Serialization
{
	/// <summary>
	/// Binary serializer. It provide a fast and memory efficient way to serialize data into binary representation.
	/// </summary>
	/// <remarks>This class is pure C# and is compatible with all platforms, including hololens.</remarks>
	public partial class Binary
	{
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
				if(!string.IsNullOrEmpty(raw_complete_type))
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
		public const float minResizeOffset = 0.1f;

		private LinkedList<byte[]> available = new LinkedList<byte[]>();
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
	}
}
