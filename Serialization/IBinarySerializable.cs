namespace CLARTE.Serialization
{
	public interface IBinarySerializable
	{
		// Class implementing IBinarySerializable MUST have a default constructor, otherwise deserialization will fail.

		uint FromBytes(Binary serializer, Binary.Buffer buffer, uint start);

		uint ToBytes(Binary serializer, ref Binary.Buffer buffer, uint start);
	}
}
