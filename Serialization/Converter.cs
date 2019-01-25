using System.Runtime.InteropServices;

namespace CLARTE.Serialization
{
    // C# implementations are always little endian. Therefore, we will not waste resources to do
    // endianness conversions as it is probably always useless. Fix it if you have to use
    // big endian architecture.

    // Store different types values at the same offset. Therefore, all fields share the same bytes

    [StructLayout(LayoutKind.Explicit)]
    public struct Converter16
    {
        [FieldOffset(0)]
        public byte Byte1;

        [FieldOffset(sizeof(byte))]
        public byte Byte2;

        [FieldOffset(0)]
        public char Char;

        [FieldOffset(0)]
        public short Short;

        public Converter16(byte value1, byte value2)
        {
            Short = 0;
            Char = (char) 0;
            Byte1 = value1;
            Byte2 = value2;
        }

        public Converter16(char value)
        {
            Byte1 = 0;
            Byte2 = 0;
            Short = 0;
            Char = value;
        }

        public Converter16(short value)
        {
            Byte1 = 0;
            Byte2 = 0;
            Char = (char) 0;
            Short = value;
        }

        public static implicit operator char(Converter16 c)
        {
            return c.Char;
        }

        public static implicit operator Converter16(char v)
        {
            return new Converter16(v);
        }

        public static implicit operator short(Converter16 c)
        {
            return c.Short;
        }

        public static implicit operator Converter16(short v)
        {
            return new Converter16(v);
        }

        public static implicit operator ushort(Converter16 c)
        {
            return (ushort) c.Short;
        }

        public static implicit operator Converter16(ushort v)
        {
            return new Converter16((short) v);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Converter32
    {
        [FieldOffset(0)]
        public byte Byte1;

        [FieldOffset(sizeof(byte))]
        public byte Byte2;

        [FieldOffset(2 * sizeof(byte))]
        public byte Byte3;

        [FieldOffset(3 * sizeof(byte))]
        public byte Byte4;

        [FieldOffset(0)]
        public int Int;

        [FieldOffset(0)]
        public float Float;

        public Converter32(byte value1, byte value2, byte value3, byte value4)
        {
            Int = 0;
            Float = 0;
            Byte1 = value1;
            Byte2 = value2;
            Byte3 = value3;
            Byte4 = value4;
        }

        public Converter32(int value)
        {
            Byte1 = 0;
            Byte2 = 0;
            Byte3 = 0;
            Byte4 = 0;
            Float = 0;
            Int = value;
        }

        public Converter32(float value)
        {
            Byte1 = 0;
            Byte2 = 0;
            Byte3 = 0;
            Byte4 = 0;
            Int = 0;
            Float = value;
        }

        public static implicit operator int(Converter32 c)
        {
            return c.Int;
        }

        public static implicit operator Converter32(int v)
        {
            return new Converter32(v);
        }

        public static implicit operator uint(Converter32 c)
        {
            return (uint) c.Int;
        }

        public static implicit operator Converter32(uint v)
        {
            return new Converter32((int) v);
        }

        public static implicit operator float(Converter32 c)
        {
            return c.Float;
        }

        public static implicit operator Converter32(float v)
        {
            return new Converter32(v);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Converter64
    {
        [FieldOffset(0)]
        public int Int1;

        [FieldOffset(sizeof(int))]
        public int Int2;

        [FieldOffset(0)]
        public long Long;

        [FieldOffset(0)]
        public double Double;

        public Converter64(int value1, int value2)
        {
            Long = 0;
            Double = 0;
            Int1 = value1;
            Int2 = value2;
        }

        public Converter64(long value)
        {
            Int1 = 0;
            Int2 = 0;
            Double = 0;
            Long = value;
        }

        public Converter64(double value)
        {
            Int1 = 0;
            Int2 = 0;
            Long = 0;
            Double = value;
        }

        public static implicit operator long(Converter64 c)
        {
            return c.Long;
        }

        public static implicit operator Converter64(long v)
        {
            return new Converter64(v);
        }

        public static implicit operator ulong(Converter64 c)
        {
            return (ulong) c.Long;
        }

        public static implicit operator Converter64(ulong v)
        {
            return new Converter64((long) v);
        }

        public static implicit operator double(Converter64 c)
        {
            return c.Double;
        }

        public static implicit operator Converter64(double v)
        {
            return new Converter64(v);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Converter128
    {
        [FieldOffset(0)]
        public int Int1;

        [FieldOffset(sizeof(int))]
        public int Int2;

        [FieldOffset(2 * sizeof(int))]
        public int Int3;

        [FieldOffset(3 * sizeof(int))]
        public int Int4;

        [FieldOffset(0)]
        public decimal Decimal;

        public Converter128(int value1, int value2, int value3, int value4)
        {
            Decimal = 0;
            Int1 = value1;
            Int2 = value2;
            Int3 = value3;
            Int4 = value4;
        }

        public Converter128(decimal value)
        {
            Int1 = 0;
            Int2 = 0;
            Int3 = 0;
            Int4 = 0;
            Decimal = value;
        }

        public static implicit operator decimal(Converter128 c)
        {
            return c.Decimal;
        }

        public static implicit operator Converter128(decimal v)
        {
            return new Converter128(v);
        }
    }
}
