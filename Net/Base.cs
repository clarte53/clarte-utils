using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace CLARTE.Net
{
    public class Base : MonoBehaviour
    {
        [StructLayout(LayoutKind.Explicit)]
        protected struct Converter
        {
            [FieldOffset(0)]
            public byte Byte1;

            [FieldOffset(1)]
            public byte Byte2;

            [FieldOffset(2)]
            public byte Byte3;

            [FieldOffset(3)]
            public byte Byte4;

            [FieldOffset(0)]
            public int Int;

            [FieldOffset(0)]
            public uint UInt;

            public Converter(byte b1, byte b2, byte b3, byte b4)
            {
                Int = 0;
                UInt = 0;
                Byte1 = b1;
                Byte2 = b2;
                Byte3 = b3;
                Byte4 = b4;
            }

            public Converter(int value)
            {
                Byte1 = 0;
                Byte2 = 0;
                Byte3 = 0;
                Byte4 = 0;
                UInt = 0;
                Int = value;
            }

            public Converter(uint value)
            {
                Byte1 = 0;
                Byte2 = 0;
                Byte3 = 0;
                Byte4 = 0;
                Int = 0;
                UInt = value;
            }
        }

        [Serializable]
        public class Credentials
        {
            #region Members
            public string username;
            public string password;
            #endregion
        }

        #region Members
        protected static readonly bool isLittleEndian;
        #endregion

        #region Constructors
        static Base()
        {
            isLittleEndian = BitConverter.IsLittleEndian;
        }
        #endregion

        #region Helper serialization functions
        protected void Send(Connection connection, bool value)
        {
            connection.stream.WriteByte(value ? (byte) 1 : (byte) 0);
        }

        protected void Send(Connection connection, int value)
        {
            Converter c = new Converter(value);

            if(isLittleEndian)
            {
                connection.stream.WriteByte(c.Byte4);
                connection.stream.WriteByte(c.Byte3);
                connection.stream.WriteByte(c.Byte2);
                connection.stream.WriteByte(c.Byte1);
            }
            else
            {
                connection.stream.WriteByte(c.Byte1);
                connection.stream.WriteByte(c.Byte2);
                connection.stream.WriteByte(c.Byte3);
                connection.stream.WriteByte(c.Byte4);
            }
        }

        protected void Send(Connection connection, uint value)
        {
            Send(connection, new Converter(value).Int);
        }

        protected void Send(Connection connection, byte[] data)
        {
            Send(connection, data.Length);

            connection.stream.Write(data, 0, data.Length);
        }

        protected void Send(Connection connection, string data)
        {
            Send(connection, Encoding.UTF8.GetBytes(data));
        }

        protected bool Receive(Connection connection, out bool value)
        {
            int val = connection.stream.ReadByte();

            value = false;

            if(val >= 0)
            {
                value = (val > 0);

                return true;
            }

            return false;
        }

        protected bool Receive(Connection connection, out int value)
        {
            int v1, v2, v3, v4;

            value = 0;

            if(isLittleEndian)
            {
                v4 = connection.stream.ReadByte();
                v3 = connection.stream.ReadByte();
                v2 = connection.stream.ReadByte();
                v1 = connection.stream.ReadByte();
            }
            else
            {
                v1 = connection.stream.ReadByte();
                v2 = connection.stream.ReadByte();
                v3 = connection.stream.ReadByte();
                v4 = connection.stream.ReadByte();
            }

            if(v1 >= 0 && v2 >= 0 && v3 >= 0 && v4 >= 0)
            {
                Converter c = new Converter((byte) v1, (byte) v2, (byte) v3, (byte) v4);

                value = c.Int;

                return true;
            }

            return false;
        }

        protected bool Receive(Connection connection, out uint value)
        {
            int val;

            bool result = Receive(connection, out val);

            value = new Converter(val).UInt;

            return result;
        }

        protected bool Receive(Connection connection, out byte[] data)
        {
            int size;

            data = null;

            if(Receive(connection, out size))
            {
                data = new byte[size];

                if(connection.stream.Read(data, 0, size) == size)
                {
                    return true;
                }
                else
                {
                    data = null;
                }
            }

            return false;
        }

        protected bool Receive(Connection connection, out string data)
        {
            byte[] raw_data;

            data = null;

            if(Receive(connection, out raw_data))
            {
                data = Encoding.UTF8.GetString(raw_data);

                return true;
            }

            return false;
        }
        #endregion

        #region Helper functions
        protected void SafeDispose<T>(T value) where T : IDisposable
        {
            try
            {
                if(value != null)
                {
                    value.Dispose();
                }
            }
            catch(ObjectDisposedException)
            {
                // Already done
            }
        }
        #endregion
    }
}
