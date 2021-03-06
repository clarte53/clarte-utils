﻿#if !NETFX_CORE

using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine.Events;

namespace CLARTE.Net.Negotiation.Connection
{
	public class Udp : Base
	{
		#region Members
		protected UdpClient client;
		protected ushort localPort;
		protected ushort remotePort;
		#endregion

		#region Constructors
		public Udp(Negotiation.Base parent, Message.Negotiation.Parameters parameters, UnityAction<Base> disconnection_handler, UdpClient client, ushort local_port, ushort remote_port) : base(parent, parameters, disconnection_handler)
		{
			this.client = client;

			localPort = local_port;
			remotePort = remote_port;
		}
		#endregion

		#region Getter / Setter
		public ushort LocalPort
		{
			get
			{
				return localPort;
			}
		}

		public ushort RemotePort
		{
			get
			{
				return remotePort;
			}
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
		public override bool Connected()
		{
			return client != null;
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
					client.BeginSend(data, data.Length, FinalizeSend, new SendState { result = result, data = data });
				}
				else
				{
					throw new ArgumentNullException("client", "The connection UdpClient is not defined.");
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

						client.BeginReceive(FinalizeReceive, new ReceiveState(ip));
					}
					else
					{
						throw new ArgumentNullException("remote", "The connection remote and channel are not defined.");
					}
				}
				else
				{
					throw new ArgumentNullException("client", "The connection UdpClient is not defined.");
				}
			}
			catch(Exception e)
			{
				HandleException(e);
			}
		}
		#endregion

		#region Internal methods
		protected void FinalizeSend(IAsyncResult async_result)
		{
			SendState state = (SendState) async_result.AsyncState;

			try
			{
				int sent_length = client.EndSend(async_result);

				if(sent_length == state.data.Length)
				{
					state.result.Complete();
				}
				else
				{
					throw new ProtocolViolationException(string.Format("Can not send all data. Sent {0} bytes instead of {1}.", sent_length, state.data.Length));
				}
			}
			catch(Exception e)
			{
				state.result.Complete(e);
			}
		}

		protected void FinalizeReceive(IAsyncResult async_result)
		{
			try
			{
				ReceiveState state = (ReceiveState) async_result.AsyncState;

				byte[] data = client.EndReceive(async_result, ref state.ip);

				if(data.Length > 0)
				{
					Threads.APC.MonoBehaviourCall.Instance.Call(() => events.onReceive.Invoke(state.ip.Address, parameters.guid, parameters.channel, data));
				}

				// Wait for next data to receive
				client.BeginReceive(FinalizeReceive, state);
			}
			catch(Exception e)
			{
				HandleException(e);
			}
		}
		#endregion
	}
}

#endif // !NETFX_CORE
