using System;
using System.Collections.Generic;

namespace CLARTE.Memory
{
	/// <summary>
	/// Buffer pool. It provide an efficient way to get reusable and resizable buffers to limit allocation and garbage collection.
	/// </summary>
	/// <remarks>This class is pure C# and is compatible with all platforms, including hololens.</remarks>
	public class BufferPool
	{
		/// <summary>
		/// A buffer of bytes.
		/// </summary>
		public class Buffer<T> : IDisposable
		{
			#region Members
			private BufferPool manager;
			private uint resizeCount;
			private bool disposed;
			#endregion

			#region Getter / Setter
			/// <summary>
			/// Get the context associated with the buffer.
			/// </summary>
			public T Context { get; private set; }

			/// <summary>
			/// Get the buffer bytes data.
			/// </summary>
			public byte[] Data { get; private set; }

			/// <summary>
			/// Get the buffer occupied size.
			/// </summary>
			public uint Size { get; set; }
			#endregion

			#region Constructors
			/// <summary>
			/// Create a new buffer.
			/// </summary>
			/// <remarks>This is the shared constructors code. This constructor should never be called by itself.</remarks>
			/// <param name="manager">The associated buffer pool.</param>
			/// <param name="context">The optional context associated with the buffer.</param>
			private Buffer(BufferPool manager, T context)
			{
				this.manager = manager;

				resizeCount = 0;

				Context = context;
				Data = null;
				Size = 0;	

				disposed = false;
			}

			protected Buffer(Buffer<T> other) : this(other.manager, other.Context)
            {
				Transfert(other);
            }

			/// <summary>
			/// Create a new buffer of at least min_size bytes.
			/// </summary>
			/// <remarks>The buffer can potentially be bigger, depending on the available allocated resources.</remarks>
			/// <param name="manager">The associated buffer pool.</param>
			/// <param name="min_size">The minimal size of the buffer.</param>
			/// <param name="context">The optional context associated with the buffer.</param>
			public Buffer(BufferPool manager, uint min_size, T context = default(T)) : this(manager, context)
			{
				Data = manager.Grab(min_size);
			}

			/// <summary>
			/// Create a new buffer from existing data.
			/// </summary>
			/// <param name="manager">The associated buffer pool.</param>
			/// <param name="existing_data">The existing data.</param>
			/// <param name="context">The optional context associated with the buffer.</param>
			public Buffer(BufferPool manager, byte[] existing_data, T context = default(T)) : this(manager, context)
			{
				Size = (uint)existing_data.Length;

				Data = existing_data;
			}
			#endregion

			#region Destructor
			// Make sure that internal data get released to the buffer pool
			~Buffer()
			{
				Dispose(true);
			}
			#endregion

			#region IDisposable implementation
			/// <summary>
			/// Dispose of buffer and return associated ressources to the pool.
			/// </summary>
			/// <param name="disposing">If true, release the memory to the pool.</param>
			private void Dispose(bool disposing)
			{
				if (!disposed)
				{
					if (disposing)
					{
						// TODO: delete managed state (managed objects).
						manager?.Release(Data);
					}

					// TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
					(Context as IDisposable)?.Dispose();

					// TODO: set fields of large size with null value.
					Context = default(T);
					Size = 0;
					Data = null;

					resizeCount = 0;
					manager = null;

					disposed = true;
				}
			}

			/// <summary>
			/// Dispose of the buffer. Release the allocated memory to the buffer pool for futur use.
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

			#region Mutations
			/// <summary>
			/// Transfert data ownership from other buffer to us.
			/// </summary>
			/// <remarks>After calling this method, the other buffer is disposed automatically and ownership of data is transfered to us.</remarks>
			/// <typeparam name="U">Type of context of the other buffer.</typeparam>
			/// <param name="other">Other buffer to get data from.</param>
			private void Transfert<U>(Buffer<U> other)
            {
				manager = other.manager;
				resizeCount = other.resizeCount;

				Data = other.Data;
				Size = other.Size;

				other.Data = null;

				// Release other buffer without releasing memory, as ownership is transfered to us.
				other.Dispose(false);
				GC.SuppressFinalize(other);
			}

			/// <summary>
			/// Transform current buffer into another buffer with same data but different context.
			/// </summary>
			/// <remarks>After calling this method, the current buffer is disposed automatically and ownership of data is transfered to the new buffer.</remarks>
			/// <typeparam name="U">The type of the new context.</typeparam>
			/// <param name="context">The new context to use.</param>
			/// <returns></returns>
			public Buffer<U> Mutate<U>(U context)
			{
				Buffer<U> result = new Buffer<U>(manager, context);

				result.Transfert(this);

				return result;
			}

			/// <summary>
			/// Transform current buffer into another buffer with same data but different context.
			/// </summary>
			/// <remarks>After calling this method, the current buffer is disposed automatically and ownership of data is transfered to the new buffer.</remarks>
			/// <typeparam name="U">The type of the new context.</typeparam>
			/// <param name="context_converter">A function to convert the old context into the new one.</param>
			/// <returns></returns>
			public Buffer<U> Mutate<U>(Func<T, U> context_converter)
			{
				return Mutate(context_converter != null ? context_converter(Context) : default(U));
			}
			#endregion

			#region Resize
			/// <summary>
			/// Resize a buffer to a new size of at least min_size.
			/// </summary>
			/// <remarks>The buffer can potentially be bigger, depending on the available allocated resources. After calling this method, the current buffer is disposed automatically and ownership of data is transfered to the new buffer.</remarks>
			/// <param name="min_size">The new minimal size of the new buffer.</param>
			public Buffer<T> Resize(uint min_size)
			{
				if (Data.Length >= min_size)
				{
					return this;
				}
				else // Buffer too small: resize
				{
					// Get how much memory we need. The idea is to reduce the need of further resizes down the road
					// for buffers that are frequently resized, while avoiding to get too much memory for buffers
					// of relatively constant size. Therefore, we allocate at least the size needed, plus an offset
					// that depends on the number of times this buffer has been resized, as well as the relative
					// impact of this resize (to avoid allocating huge amount of memory if a resize increase drastically
					// the size of the buffer. Hopefully, this algorithm should allow a fast convergence to the
					// ideal buffer size. However, keep in mind that resizes should be a last resort and should be avoided
					// when possible.
					uint current_size = (uint)Data.Length;
					float growth = Math.Max(1f - ((float)min_size) / current_size, minResizeOffset);
					uint new_size = min_size + (uint)(resizeCount * growth * min_size);

					// Get a new buffer of sufficient size
					Buffer<T> new_buffer = new Buffer<T>(manager, new_size, Context);

					// Increment resize count
					new_buffer.resizeCount = resizeCount + 1;

					// Copy old buffer content into new one
					Array.Copy(Data, new_buffer.Data, Data.Length);
					new_buffer.Size = Size;

					// Release old buffer
					// Actually, do not call dispose for this buffer! If we do, it will be added back to the pool
					// of available buffers and the allocated memory could increase drastically over time.
					// Instead, we purposefully ignore to release it. Therefore, the memory will be released when
					// the buffer gets out of scope, i.e. at the end of this function.
					Dispose(false);
					GC.SuppressFinalize(this);

					// Switch buffers
					return new_buffer;
				}
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
		/// <typeparam name="T">The type of the context associated with the buffer.</typeparam>
		/// <param name="min_size">The minimal size of the buffer.</param>
		/// <param name="context">The context associated to the buffer.</param>
		/// <returns>A buffer.</returns>
		public Buffer<T> GetBuffer<T>(uint min_size, T context = default(T))
		{
			return new Buffer<T>(this, min_size, context);
		}

		/// <summary>
		/// Get a buffer from existing data.
		/// </summary>
		/// <typeparam name="T">The type of the context associated with the buffer.</typeparam>
		/// <param name="data">The existing data.</param>
		/// <param name="context">The context associated to the buffer.</param>
		/// <returns>A buffer.</returns>
		public Buffer<T> GetBufferFromExistingData<T>(byte[] data, T context = default(T))
		{
			return new Buffer<T>(this, data, context);
		}

		/// <summary>
		/// Resize a buffer to a new size of at least min_size.
		/// </summary>
		/// <remarks>The buffer can potentially be bigger, depending on the available allocated resources.</remarks>
		/// <typeparam name="T">The type of the context associated with the buffer.</typeparam>
		/// <param name="buffer">The buffer to resize.</param>
		/// <param name="min_size">The new minimal size of the buffer.</param>
		public void ResizeBuffer<T>(ref Buffer<T> buffer, uint min_size)
		{
			if(buffer == null)
			{
				throw new ArgumentNullException("buffer", "Can not resize undefined buffer.");
			}
			
			buffer = buffer.Resize(min_size);
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
