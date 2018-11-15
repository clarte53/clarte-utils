using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace CLARTE.Net.Negotiation.Connection
{
    public class Tcp : Base
    {
        [StructLayout(LayoutKind.Explicit)]
        protected struct Converter
        {
            public const byte bytesSize = 4;

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

            [FieldOffset(0)]
            public ushort UShort;

            public Converter(byte b1, byte b2, byte b3, byte b4)
            {
                Int = 0;
                UInt = 0;
                UShort = 0;
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
                UShort = 0;
                Int = value;
            }

            public Converter(uint value)
            {
                Byte1 = 0;
                Byte2 = 0;
                Byte3 = 0;
                Byte4 = 0;
                Int = 0;
                UShort = 0;
                UInt = value;
            }

            public Converter(ushort value)
            {
                Byte1 = 0;
                Byte2 = 0;
                Byte3 = 0;
                Byte4 = 0;
                Int = 0;
                UInt = 0;
                UShort = value;
            }
        }

        #region Members
        protected static readonly bool isLittleEndian;

        public Threads.Result initialization;
        public TcpClient client;
        public Stream stream;
        public uint version;

        protected byte[] buffer;
        #endregion

        #region Constructors
        static Tcp()
        {
            isLittleEndian = BitConverter.IsLittleEndian;
        }

        public Tcp(TcpClient client)
        {
            this.client = client;
            stream = null;

            buffer = new byte[Converter.bytesSize];
        }
        #endregion

        #region IDisposable implementation
        protected override void Dispose(bool disposing)
        {
            if(!disposed)
            {
                if(disposing)
                {
                    // TODO: delete managed state (managed objects).

                    try
                    {
                        // Flush the stream to make sure that all sent data is effectively sent to the client
                        if(stream != null)
                        {
                            stream.Flush();
                        }
                    }
                    catch(ObjectDisposedException)
                    {
                        // Already closed
                    }

                    // Close the stream and client
                    SafeDispose(stream);
                    SafeDispose(client);
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                disposed = true;
            }
        }
        #endregion

        #region Base class implementation
        public override IPAddress GetRemoteAddress()
        {
            IPAddress address = null;

            if(client != null)
            {
                address = ((IPEndPoint) client.Client.RemoteEndPoint).Address;
            }

            return address;
        }

        public override Threads.Result SendAsync(byte[] data)
        {
            Threads.Result result = new Threads.Result();

            Converter c = new Converter(data.Length);

            if(isLittleEndian)
            {
                buffer[0] = c.Byte4;
                buffer[1] = c.Byte3;
                buffer[2] = c.Byte2;
                buffer[3] = c.Byte1;
            }
            else
            {
                buffer[0] = c.Byte1;
                buffer[1] = c.Byte2;
                buffer[2] = c.Byte3;
                buffer[3] = c.Byte4;
            }

            stream.BeginWrite(buffer, 0, buffer.Length, FinalizeSendLength, new SendData { result = result, data = data });

            return result;
        }
        #endregion

        #region Helper serialization functions
        public void Send(bool value)
        {
            if(stream != null)
            {
                stream.WriteByte(value ? (byte) 1 : (byte) 0);
            }
        }

        public void Send(ushort value)
        {
            if(stream != null)
            {
                Converter c = new Converter(value);

                if(isLittleEndian)
                {
                    stream.WriteByte(c.Byte2);
                    stream.WriteByte(c.Byte1);
                }
                else
                {
                    stream.WriteByte(c.Byte1);
                    stream.WriteByte(c.Byte2);
                }
            }
        }

        public void Send(int value)
        {
            if(stream != null)
            {
                Converter c = new Converter(value);

                if(isLittleEndian)
                {
                    stream.WriteByte(c.Byte4);
                    stream.WriteByte(c.Byte3);
                    stream.WriteByte(c.Byte2);
                    stream.WriteByte(c.Byte1);
                }
                else
                {
                    stream.WriteByte(c.Byte1);
                    stream.WriteByte(c.Byte2);
                    stream.WriteByte(c.Byte3);
                    stream.WriteByte(c.Byte4);
                }
            }
        }

        public void Send(uint value)
        {
            Send(new Converter(value).Int);
        }

        public void Send(byte[] data)
        {
            if(stream != null)
            {
                Send(data.Length);

                stream.Write(data, 0, data.Length);
            }
        }

        public void Send(string data)
        {
            Send(Encoding.UTF8.GetBytes(data));
        }

        public bool Receive(out bool value)
        {
            value = false;

            if(stream != null)
            {
                int val = stream.ReadByte();

                if(val >= 0)
                {
                    value = (val > 0);

                    return true;
                }
            }

            return false;
        }

        public bool Receive(out ushort value)
        {
            int v1, v2;

            value = 0;

            if(stream != null)
            {
                if(isLittleEndian)
                {
                    v2 = stream.ReadByte();
                    v1 = stream.ReadByte();
                }
                else
                {
                    v1 = stream.ReadByte();
                    v2 = stream.ReadByte();
                }

                if(v1 >= 0 && v2 >= 0)
                {
                    Converter c = new Converter((byte) v1, (byte) v2, 0, 0);

                    value = c.UShort;

                    return true;
                }
            }

            return false;
        }

        public bool Receive(out int value)
        {
            int v1, v2, v3, v4;

            value = 0;

            if(stream != null)
            {
                if(isLittleEndian)
                {
                    v4 = stream.ReadByte();
                    v3 = stream.ReadByte();
                    v2 = stream.ReadByte();
                    v1 = stream.ReadByte();
                }
                else
                {
                    v1 = stream.ReadByte();
                    v2 = stream.ReadByte();
                    v3 = stream.ReadByte();
                    v4 = stream.ReadByte();
                }

                if(v1 >= 0 && v2 >= 0 && v3 >= 0 && v4 >= 0)
                {
                    Converter c = new Converter((byte) v1, (byte) v2, (byte) v3, (byte) v4);

                    value = c.Int;

                    return true;
                }
            }

            return false;
        }

        public bool Receive(out uint value)
        {
            int val;

            bool result = Receive(out val);

            value = new Converter(val).UInt;

            return result;
        }

        public bool Receive(out byte[] data)
        {
            int size;

            data = null;

            if(stream != null)
            {
                if(Receive(out size))
                {
                    data = new byte[size];

                    if(stream.Read(data, 0, size) == size)
                    {
                        return true;
                    }
                    else
                    {
                        data = null;
                    }
                }
            }

            return false;
        }

        public bool Receive(out string data)
        {
            byte[] raw_data;

            data = null;

            if(Receive(out raw_data))
            {
                data = Encoding.UTF8.GetString(raw_data);

                return true;
            }

            return false;
        }
        #endregion

        #region Internal methods
        protected void FinalizeSendLength(IAsyncResult async_result)
        {
            SendData send = (SendData) async_result.AsyncState;

            stream.EndWrite(async_result);

            stream.BeginWrite(send.data, 0, send.data.Length, FinalizeSendData, send);
        }

        protected void FinalizeSendData(IAsyncResult async_result)
        {
            SendData send = (SendData) async_result.AsyncState;

            stream.EndWrite(async_result);

            send.result.Complete();
        }
        #endregion
    }

    public class TcpWithChannel : Tcp
    {
        #region Members
        public int channel;
        #endregion

        #region Constructors
        public TcpWithChannel(TcpClient client, int channel) : base(client)
        {
            this.channel = channel;
        }
        #endregion
    }
}
