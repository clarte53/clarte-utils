#if !NETFX_CORE

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine.Events;
using CLARTE.Serialization;

namespace CLARTE.Net.Negotiation.Connection
{
    public class Tcp : Base
    {
		#region Members
		protected const ushort headerSize = 5;

		public Threads.IResult initialization;
        public TcpClient client;
        public Stream stream;
        public uint version;

		protected byte[] headerBuffer;
		protected byte[] readBuffer;
        protected byte[] writeBuffer;
        #endregion

        #region Constructors
        public Tcp(Negotiation.Base parent, Message.Negotiation.Parameters parameters, UnityAction<Base> disconnection_handler, TcpClient client) : base(parent, parameters, disconnection_handler)
        {
            this.client = client;

            stream = null;

			headerBuffer = new byte[headerSize];
			readBuffer = new byte[sizeof(int)];
            writeBuffer = new byte[sizeof(int)];
        }
        #endregion

        #region IDisposable implementation
        protected override void DisposeInternal(bool disposing)
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
        }
		#endregion

		#region Base class implementation
		public override bool Connected()
		{
			return client != null && stream != null && client.Connected;
		}

		public override IPAddress GetRemoteAddress()
        {
            IPAddress address = null;

            if(client != null)
            {
                address = ((IPEndPoint) client.Client.RemoteEndPoint).Address;
            }

            return address;
        }

        protected override void SendAsync(Threads.Result result, byte[] data)
        {
            try
            {
                if(client != null)
                {
                    Converter32 c = new Converter32(data.Length);

                    writeBuffer[0] = c.Byte1;
                    writeBuffer[1] = c.Byte2;
                    writeBuffer[2] = c.Byte3;
                    writeBuffer[3] = c.Byte4;

                    stream.BeginWrite(writeBuffer, 0, writeBuffer.Length, FinalizeSendLength, new SendState { result = result, data = data });
                }
                else
                {
                    throw new ArgumentNullException("client", "The connection tcpClient is not defined.");
                }
            }
            catch(Exception e)
            {
                result.Complete(e);
            }
        }

        protected override void ReceiveAsync()
        {
			try
			{
				if(client != null)
				{
					if(parameters.guid != Guid.Empty)
					{
						IPEndPoint ip = (IPEndPoint) client.Client.RemoteEndPoint;

						ReceiveState state = new ReceiveState(ip);

						state.Set(readBuffer);

						stream.BeginRead(state.data, state.offset, state.MissingDataLength, FinalizeReceiveLength, state);
					}
					else
					{
						throw new ArgumentNullException("remote", "The connection remote and channel are not defined.");
					}
				}
				else
				{
					throw new ArgumentNullException("client", "The connection tcpClient is not defined.");
				}
			}
			catch(Exception e)
			{
				HandleException(e);
			}
		}
		#endregion

		#region Helper serialization functions
		public void Send(Message.Base message)
		{
			if(stream != null && parent != null)
			{
				Binary.Buffer buffer = parent.Serializer.GetBuffer(256);

				uint written = parent.Serializer.ToBytes(ref buffer, 0, message);

				Converter32 converter = new Converter32(written);

				stream.WriteByte(Pattern.Factory<Message.Base, byte>.Get(message.GetType()));
				stream.WriteByte(converter.Byte1);
				stream.WriteByte(converter.Byte2);
				stream.WriteByte(converter.Byte3);
				stream.WriteByte(converter.Byte4);
				stream.Write(buffer.Data, 0, (int) written);
			}
		}

		public bool Receive(out Message.Base message)
		{
			uint read = 0;
			uint size = 0;

			message = null;

			if(stream != null && parent != null)
			{
				if(ReceiveData(stream, headerBuffer, headerSize))
				{
					message = Pattern.Factory<Message.Base, byte>.CreateInstance(headerBuffer[0]);

					size = new Converter32(headerBuffer[1], headerBuffer[2], headerBuffer[3], headerBuffer[4]);

					Binary.Buffer buffer = parent.Serializer.GetBuffer(size);

					if(ReceiveData(stream, buffer.Data, size))
					{
						read = parent.Serializer.FromBytesOverwrite(buffer, 0, message);
					}
				}
			}

			return (message != null && read == size);
		}

		protected bool ReceiveData(Stream stream, byte[] buffer, uint size)
		{
			int received = 0;

			try
			{
				while(received < size)
				{
					received += stream.Read(buffer, received, (int) (size - received));
				}
			}
			catch(Exception e)
			{
				HandleException(e);

				return false;
			}

			return true;
		}
        #endregion

        #region Internal methods
        protected void FinalizeSendLength(IAsyncResult async_result)
        {
            SendState state = (SendState) async_result.AsyncState;

            try
            {
				if(state.data.Length > 0)
				{
					stream.EndWrite(async_result);

					stream.BeginWrite(state.data, 0, state.data.Length, FinalizeSendData, state);
				}
				else
				{
					FinalizeSendData(async_result);
				}
            }
            catch(Exception e)
            {
                state.result.Complete(e);
            }
        }

        protected void FinalizeSendData(IAsyncResult async_result)
        {
            SendState state = (SendState) async_result.AsyncState;

            try
            {
                stream.EndWrite(async_result);

                stream.Flush();

                state.result.Complete();
            }
            catch(Exception e)
            {
                state.result.Complete(e);
            }
        }

        protected void FinalizeReceive(IAsyncResult async_result, Action<ReceiveState> callback)
        {
			try
			{
				ReceiveState state = (ReceiveState) async_result.AsyncState;

				int read_length = stream.EndRead(async_result);

				int missing = state.MissingDataLength;

				state.offset += read_length;

				if(read_length == missing)
				{
					// We got all the data: pass it back to the application
					callback(state);
				}
				else if(read_length == 0)
				{
					// Connection is closed. Dispose of resources
					Threads.APC.MonoBehaviourCall.Instance.Call(Close);
				}
				else if(read_length < missing)
				{
					Threads.APC.MonoBehaviourCall.Instance.Call(() => events.onReceiveProgress.Invoke(address, parameters.guid, parameters.channel, ((float) state.offset) / ((float) state.data.Length)));

					// Get the remaining data
					stream.BeginRead(state.data, state.offset, state.MissingDataLength, FinalizeReceiveData, state);
				}
				else
				{
					throw new ProtocolViolationException(string.Format("Received too much bytes from message. Received {0} bytes instead of {1}.", state.offset + read_length, state.data.Length));
				}
			}
			catch(Exception e)
			{
				HandleException(e);
			}
		}

        protected void FinalizeReceiveLength(IAsyncResult async_result)
        {
            FinalizeReceive(async_result, state =>
            {
                Converter32 c = new Converter32(state.data[0], state.data[1], state.data[2], state.data[3]);

				if(c.Int > 0)
				{
					state.Set(new byte[c.Int]);

					stream.BeginRead(state.data, state.offset, state.MissingDataLength, FinalizeReceiveData, state);
				}
				else // Notning to read anymore
				{
					state.Set(readBuffer);

					// Wait for next message
					stream.BeginRead(state.data, state.offset, state.MissingDataLength, FinalizeReceiveLength, state);
				}
            });
        }

        protected void FinalizeReceiveData(IAsyncResult async_result)
        {
            FinalizeReceive(async_result, state =>
            {
                byte[] data = state.data; // Otherwise the call to state.data in unity thread will be evaluated to null, because of the weird catching of parameters of lambdas

                Threads.APC.MonoBehaviourCall.Instance.Call(() => events.onReceive.Invoke(state.ip.Address, parameters.guid, parameters.channel, data));

                state.Set(readBuffer);

                // Wait for next message
                stream.BeginRead(state.data, state.offset, state.MissingDataLength, FinalizeReceiveLength, state);
            });
        }
        #endregion
    }
}

#endif // !NETFX_CORE
