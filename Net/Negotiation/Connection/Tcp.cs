﻿using System;
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

        protected byte[] readBuffer;
        protected byte[] writeBuffer;
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

            readBuffer = new byte[Converter.bytesSize];
            writeBuffer = new byte[Converter.bytesSize];
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

            if(client != null)
            {
                Converter c = new Converter(data.Length);

                if(isLittleEndian)
                {
                    writeBuffer[0] = c.Byte4;
                    writeBuffer[1] = c.Byte3;
                    writeBuffer[2] = c.Byte2;
                    writeBuffer[3] = c.Byte1;
                }
                else
                {
                    writeBuffer[0] = c.Byte1;
                    writeBuffer[1] = c.Byte2;
                    writeBuffer[2] = c.Byte3;
                    writeBuffer[3] = c.Byte4;
                }

                stream.BeginWrite(writeBuffer, 0, writeBuffer.Length, FinalizeSendLength, new SendState { result = result, data = data });
            }
            else
            {
                result.Complete(new ArgumentNullException("client", "The connection tcpClient is not defined."));
            }

            return result;
        }

        protected override void ReceiveAsync()
        {
            if(client != null)
            {
                if(channel.HasValue)
                {
                    IPEndPoint ip = (IPEndPoint) client.Client.RemoteEndPoint;

                    stream.BeginRead(readBuffer, 0, readBuffer.Length, FinalizeReceiveLength, new ReceiveState { ip = ip, data = null });
                }
                else
                {
                    throw new ArgumentNullException("channel", "The connection channel is not defined.");
                }
            }
            else
            {
                throw new ArgumentNullException("client", "The connection tcpClient is not defined.");
            }
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
            SendState state = (SendState) async_result.AsyncState;

            stream.EndWrite(async_result);

            stream.BeginWrite(state.data, 0, state.data.Length, FinalizeSendData, state);
        }

        protected void FinalizeSendData(IAsyncResult async_result)
        {
            SendState state = (SendState) async_result.AsyncState;

            stream.EndWrite(async_result);

            state.result.Complete();
        }

        protected void FinalizeReceiveLength(IAsyncResult async_result)
        {
            ReceiveState state = (ReceiveState) async_result.AsyncState;

            int read_length = stream.EndRead(async_result);

            if(read_length == readBuffer.Length)
            {
                byte b1, b2, b3, b4;

                if(isLittleEndian)
                {
                    b4 = readBuffer[0];
                    b3 = readBuffer[1];
                    b2 = readBuffer[2];
                    b1 = readBuffer[3];
                }
                else
                {
                    b1 = readBuffer[0];
                    b2 = readBuffer[1];
                    b3 = readBuffer[2];
                    b4 = readBuffer[3];
                }

                Converter c = new Converter(b1, b2, b3, b4);

                state.data = new byte[c.Int];

                stream.BeginRead(state.data, 0, state.data.Length, FinalizeReceiveData, state);
            }
            else
            {
                throw new ProtocolViolationException(string.Format("Can not receive all bytes from buffer length. Received {0} bytes instead of {1}.", read_length, readBuffer.Length));
            }
        }

        protected void FinalizeReceiveData(IAsyncResult async_result)
        {
            ReceiveState state = (ReceiveState) async_result.AsyncState;

            int read_length = stream.EndRead(async_result);

            if(read_length == state.data.Length)
            {
                onReceive.Invoke(state.ip.Address, channel.Value, state.data);

                state.data = null;

                // Wait for next message
                stream.BeginRead(readBuffer, 0, readBuffer.Length, FinalizeReceiveLength, state);
            }
            else
            {
                throw new ProtocolViolationException(string.Format("Can not receive all bytes from message. Received {0} bytes instead of {1}.", read_length, state.data.Length));
            }
        }
        #endregion
    }
}
