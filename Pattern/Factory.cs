﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CLARTE.Pattern
{
	public abstract class Factory
	{
		#region Converters
		protected static void CorrectEndianness(byte[] hash, ushort size)
		{
			// Test endianness
			if(!BitConverter.IsLittleEndian)
			{
				// Reverse bytes order of used bytes to compensate for endianness
				ushort half_size = (ushort) (size / 2);

				for(ushort i = 0; i < half_size; i++)
				{
					ushort j = (ushort) (size - i - 1);

					byte a = hash[i];
					byte b = hash[j];

					hash[i] = b;
					hash[j] = a;
				}
			}
		}

		public static byte ByteConverter(byte[] hash)
		{
			return hash[0];
		}

		public static sbyte SByteConverter(byte[] hash)
		{
			return (sbyte) hash[0];
		}

		public static short Int16Converter(byte[] hash)
		{
			CorrectEndianness(hash, sizeof(short));

			return BitConverter.ToInt16(hash, 0);
		}

		public static ushort UInt16Converter(byte[] hash)
		{
			CorrectEndianness(hash, sizeof(ushort));

			return BitConverter.ToUInt16(hash, 0);
		}

		public static int Int32Converter(byte[] hash)
		{
			CorrectEndianness(hash, sizeof(int));

			return BitConverter.ToInt32(hash, 0);
		}

		public static uint UInt32Converter(byte[] hash)
		{
			CorrectEndianness(hash, sizeof(uint));

			return BitConverter.ToUInt32(hash, 0);
		}

		public static long Int64Converter(byte[] hash)
		{
			CorrectEndianness(hash, sizeof(long));

			return BitConverter.ToInt64(hash, 0);
		}

		public static ulong UInt64Converter(byte[] hash)
		{
			CorrectEndianness(hash, sizeof(ulong));

			return BitConverter.ToUInt64(hash, 0);
		}
		#endregion
	}

	/// <summary>
	/// Factory of objects derived from the same base, using platform independant repetable ids.
	/// </summary>
	/// <typeparam name="T">The base class of objects generated by the factory.</typeparam>
	/// <typeparam name="U">The type to use for ids.</typeparam>
	public class Factory<T, U> : Factory
	{
		public class InitializationException : Exception
		{
			public InitializationException(string message) : base(message)
			{

			}
		}

		#region Members
		protected static Dictionary<U, Type> id2type;
		protected static Dictionary<Type, U> type2id;
		protected static bool initialized;
		#endregion

		#region Constructors
		public static void Initialize(Func<byte[], U> converter)
		{
			if(!initialized)
			{
				id2type = new Dictionary<U, Type>();
				type2id = new Dictionary<Type, U>();

				IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(s => s.GetTypes())
					.Where(p => typeof(T).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

				using(MD5 md5 = MD5.Create())
				{
					foreach(Type type in types)
					{
						// Get the md5 hash of the fully qualified type name and convert it into an id value
						U type_id = converter(md5.ComputeHash(Encoding.UTF8.GetBytes(type.ToString())));

						// Register the association between the type and the computed id
						id2type.Add(type_id, type);
						type2id.Add(type, type_id);
					}
				}

				initialized = true;
			}
		}
		#endregion

		#region Public methods
		public static Type Get(U id)
		{
			Type type;

			if(!initialized)
			{
				throw new InitializationException("Invalid use of uninitialized factory.");
			}

			if(type2id == null || !id2type.TryGetValue(id, out type))
			{
				throw new ArgumentException(string.Format("Invalid id '{0}'.", id), "id");
			}

			return type;
		}

		public static U Get(Type type)
		{
			U id;

			if(!initialized)
			{
				throw new InitializationException("Invalid use of uninitialized factory.");
			}

			if(type2id == null || !type2id.TryGetValue(type, out id))
			{
				throw new ArgumentException(string.Format("Invalid Type '{0}'.", type), "type");
			}

			return id;
		}

		public static T CreateInstance(U id, params object[] args)
		{
			return CreateInstance(Get(id), args);
		}

		public static T CreateInstance(Type type, params object[] args)
		{
			if(!initialized)
			{
				throw new InitializationException("Invalid use of uninitialized factory.");
			}

			if(!typeof(T).IsAssignableFrom(type))
			{
				throw new ArgumentException(string.Format("Invalid Type '{0}'. Not a derived type of '{1}'.", type, typeof(T)));
			}

			return (T) Activator.CreateInstance(type, args);
		}
		#endregion
	}
}
