using System;
using System.Net;
using System.Net.Sockets;

namespace CLARTE.Net.Negotiation.Connection
{
    public class Udp : Base
    {
        #region Members
        public UdpClient client;

        protected Negotiation.Base parent;
        #endregion

        #region Constructors
        public Udp(Negotiation.Base parent, UdpClient client, TimeSpan heartbeat) : base(heartbeat)
        {
            this.parent = parent;
            this.client = client;
        }
        #endregion

        #region IDisposable implementation
        protected override void DisposeInternal(bool disposing)
        {
            if(disposing)
            {
                // TODO: delete managed state (managed objects).

                ushort port = 0;

                if(client != null)
                {
                    port = (ushort) ((IPEndPoint) client.Client.LocalEndPoint).Port;
                }

                // Close the client
                SafeDispose(client);

                // Release the used port
                if(parent != null && port != 0)
                {
                    parent.ReleasePort(port);
                }
            }

            // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
            // TODO: set fields of large size with null value.
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

        public override bool Connected()
        {
            return client != null;
        }

        protected override Threads.Result SendAsync(Threads.Result result, byte[] data)
        {
            if(client != null)
            {
                client.BeginSend(data, data.Length, FinalizeSend, new SendState { result = result, data = data });
            }
            else
            {
                result.Complete(new ArgumentNullException("client", "The connection UdpClient is not defined."));
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

                    client.BeginReceive(FinalizeReceive, new ReceiveState(ip));
                }
                else
                {
                    throw new ArgumentNullException("channel", "The connection channel is not defined.");
                }
            }
            else
            {
                throw new ArgumentNullException("client", "The connection UdpClient is not defined.");
            }
        }
        #endregion

        #region Internal methods
        protected void FinalizeSend(IAsyncResult async_result)
        {
            SendState state = (SendState) async_result.AsyncState;

            int sent_length = client.EndSend(async_result);

            if(sent_length == state.data.Length)
            {
                state.result.Complete();
            }
            else
            {
                state.result.Complete(new ProtocolViolationException(string.Format("Can not send all data. Sent {0} bytes instead of {1}.", sent_length, state.data.Length)));
            }
        }

        protected void FinalizeReceive(IAsyncResult async_result)
        {
            ReceiveState state = (ReceiveState) async_result.AsyncState;

            byte[] data = client.EndReceive(async_result, ref state.ip);

            Threads.APC.MonoBehaviourCall.Instance.Call(() => onReceive.Invoke(state.ip.Address, channel.Value, data));

            // Wait for next data to receive
            client.BeginReceive(FinalizeReceive, state);
        }
        #endregion
    }
}
