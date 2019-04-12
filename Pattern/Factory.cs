using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Factory of objects derived from the same base, using platform independant repetable ids.
/// </summary>
/// <typeparam name="T"></typeparam>
public class Factory<T>
{
	#region Members
	protected static Dictionary<ulong, Type> id2type;
	protected static Dictionary<Type, ulong> type2id;
	#endregion

	#region Constructors
	static Factory()
	{
		id2type = new Dictionary<ulong, Type>();
		type2id = new Dictionary<Type, ulong>();

		IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(s => s.GetTypes())
			.Where(p => typeof(T).IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

		using(MD5 md5 = MD5.Create())
		{
			foreach(Type type in types)
			{
				const uint id_byte_size = sizeof(ulong);

				byte[] hash = new byte[id_byte_size];

				// Get the first bytes of the md5 hash of the fully qualified type name
				Array.Copy(md5.ComputeHash(Encoding.ASCII.GetBytes(type.ToString())), hash, id_byte_size);

				// Test endianness
				if(!BitConverter.IsLittleEndian)
				{
					Array.Reverse(hash);
				}

				// Convert the first bytes into an unsigned 64 bits integer value
				ulong type_id = BitConverter.ToUInt64(hash, 0);

				// Register the association between the type and the computed id
				id2type.Add(type_id, type);
				type2id.Add(type, type_id);
			}
		}
	}
	#endregion

	#region Public methods
	public static Type Get(ulong id)
	{
		Type type;

		if(type2id != null && id2type.TryGetValue(id, out type))
		{
			return type;
		}
		else
		{
			throw new ArgumentException(string.Format("Invalid id '{0}'.", id), "id");
		}
	}

	public static ulong Get(Type type)
	{
		ulong id;

		if(type2id != null && type2id.TryGetValue(type, out id))
		{
			return id;
		}
		else
		{
			throw new ArgumentException(string.Format("Invalid Type '{0}'.", type), "type");
		}
	}

	public static T CreateInstance(ulong id, params object[] args)
	{
		return CreateInstance(Get(id), args);
	}

	public static T CreateInstance(Type type, params object[] args)
	{
		if(!typeof(T).IsAssignableFrom(type))
		{
			throw new ArgumentException(string.Format("Invalid Type '{0}'. Not a derived type of '{1}'.", type, typeof(T)));
		}

		return (T) Activator.CreateInstance(type, args);
	}
	#endregion
}
