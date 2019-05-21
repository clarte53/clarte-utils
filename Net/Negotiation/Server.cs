#if !NETFX_CORE

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using UnityEngine;

namespace CLARTE.Net.Negotiation
{
    public class Server : Base<ServerChannel>
    {
        #region Members
        public const uint maxSupportedVersion = 1;

        public TextAsset certificate;
        public uint port;

        protected Threads.Thread listenerThread;
		protected Threads.Thread monitorThread;
		protected TcpListener listener;
        protected X509Certificate2 serverCertificate;
        protected ManualResetEvent stopEvent;
        #endregion

        #region IDisposable implementation
        protected override void Dispose(bool disposing)
        {
            if(state != State.DISPOSED)
            {
                state = State.CLOSING;

                if(disposing)
                {
                    // TODO: delete managed state (managed objects).

                    listener.Stop();

                    stopEvent.Set();

                    CloseInitializedConnections();

                    CloseOpenedChannels();

					CloseMonitor();

					monitorThread.Join();
                    listenerThread.Join();

                    stopEvent.Close();
                }

                // TODO: free unmanaged resources (unmanaged objects) and replace finalizer below.
                // TODO: set fields of large size with null value.

                state = State.DISPOSED;
            }
        }
		#endregion

		#region Base implementation
		protected override void Reconnect(Connection.Base connection)
		{
			
		}
		#endregion

		#region MonoBehaviour callbacks
		protected override void Awake()
        {
            base.Awake();

            state = State.INITIALIZING;

            stopEvent = new ManualResetEvent(false);

            serverCertificate = null;

            // Should we use an encrypted channel?
            if(certificate != null && certificate.bytes.Length > 0)
            {
                string tmp_file = string.Format("{0}{1}{2}", Application.temporaryCachePath, Path.DirectorySeparatorChar, certificate.name);

                File.WriteAllBytes(tmp_file, certificate.bytes);

                try
                {
#if !UNITY_WSA
                    // Import the certificate
                    serverCertificate = new X509Certificate2(tmp_file);
#else
                    // At the moment, SslStream is not working on Hololens platform.
                    // Indeed, at the moment, player capabilities does not provide a way to authorize access to the trusted root certificates store.
                    throw new NotSupportedException("SSL streams are not supported on Hololens.");
#endif
                }
                catch(Exception)
                {
                    Debug.LogWarningFormat("Invalid certificate file '{0}'. Encryption is disabled.", certificate.name);

                    serverCertificate = null;
                }

                File.Delete(tmp_file);
            }

			monitorThread = new Threads.Thread(MonitorWorker);

			listener = new TcpListener(IPAddress.Any, (int) port);
            listener.Start();

            listenerThread = new Threads.Thread(Listen);
            listenerThread.Start();

            Debug.LogFormat("Started server on port {0}", port);

            state = State.RUNNING;
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if(channels != null)
            {
                foreach(ServerChannel channel in channels)
                {
                    if(!channel.disableHeartbeat && channel.heartbeat == 0)
                    {
                        channel.type = Channel.Type.TCP;
                        channel.heartbeat = 2f;
                    }
                }
            }
        }
        #endregion

        #region Connection methods
        protected void Listen()
        {
            while(true)
            {
                // Listen for new connections
                IAsyncResult context = listener.BeginAcceptTcpClient(AcceptClient, null);

                // Wait for next connection or exit signal
                if(WaitHandle.WaitAny(new[] { stopEvent, context.AsyncWaitHandle }) == 0)
                {
                    return;
                }
            }
        }

        protected void AcceptClient(IAsyncResult async_result)
        {
            try
            {
                if(state == State.RUNNING)
                {
					Message.Negotiation.Parameters param = new Message.Negotiation.Parameters
					{
						guid = Guid.Empty,
						channel = 0,
						heartbeat = defaultHeartbeat,
						autoReconnect = false
					};

                    // Get the new connection
                    Connection.Tcp connection = new Connection.Tcp(this, param, DisconnectionHandler, listener.EndAcceptTcpClient(async_result));

                    lock(initializedConnections)
                    {
                        initializedConnections.Add(connection);
                    }

                    connection.initialization = Threads.Tasks.Add(() => Connected(connection));
                }
            }
            catch(Exception exception)
            {
                Debug.LogErrorFormat("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
            }
        }

        protected void Connected(Connection.Tcp connection)
        {
            try
            {
                // We should be connected
                if(connection.client.Connected)
                {
                    // Get the stream associated with this connection
                    connection.stream = connection.client.GetStream();

					Message.Connection.Parameters header = new Message.Connection.Parameters
					{
						version = maxSupportedVersion,
						encrypted = (serverCertificate != null)
					};

					// Send greating message with protocol version and parameters
					connection.Send(header);

					if(serverCertificate != null)
					{
						// Create the SSL wraping stream
						connection.stream = new SslStream(connection.stream);

						// Authenticate with the client
						((SslStream) connection.stream).BeginAuthenticateAsServer(serverCertificate, Authenticated, connection);
					}
					else
					{
						// No encryption, the channel stay as is
						ValidateCredentials(connection);
					}	
                }
                else
                {
                    Debug.LogError("The connection from the client failed.");
                }
            }
            catch(DropException)
            {
                throw;
            }
            catch(Exception exception)
            {
                Debug.LogErrorFormat("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
            }
        }

        protected void Authenticated(IAsyncResult async_result)
        {
            Connection.Tcp connection = null;

            try
            {
                // Finalize the authentication as server for the SSL stream
                connection = (Connection.Tcp) async_result.AsyncState;

                ((SslStream) connection.stream).EndAuthenticateAsServer(async_result);

                ValidateCredentials(connection);
            }
            catch(DropException)
            {
                throw;
            }
            catch(Exception)
            {
                Drop(connection, "Authentication failed.");
            }
        }

        protected void ValidateCredentials(Connection.Tcp connection)
        {
			Message.Base req;

			if(connection.Receive(out req) && req.IsType<Message.Connection.Request>())
			{
				Message.Connection.Request request = (Message.Connection.Request) req;

				connection.version = request.version;

				if(connection.version < maxSupportedVersion)
				{
					Debug.LogWarningFormat("Client does not support protocol version '{0}'. Using version '{1}' instead.", maxSupportedVersion, connection.version);
				}

				Message.Connection.Validation validation = new Message.Connection.Validation();

				// Check if the credentials are valid
				if(request.username == credentials.username && request.password == credentials.password)
				{
					validation.accepted = true;

					// Notify the client that the credentials are valid
					connection.Send(validation);

					NegotiateChannels(connection);
				}
				else
				{
					string error_message = string.Format("Invalid connection credentials for user '{0}'. Dropping connection.", request.username);

					Debug.LogWarning(error_message);

					validation.accepted = false;

					// Notify the client that the credentials are wrong
					connection.Send(validation);

					// Drop the connection
					Close(connection);

					throw new DropException(error_message);
				}
			}
			else
			{
				Drop(connection, "Expected to receive negotiation connection request.");
			}
        }

		protected void NegotiateChannels(Connection.Tcp connection)
		{
			Message.Base msg;

			if(connection.Receive(out msg))
			{
				if(msg.IsType<Message.Negotiation.Start>())
				{
					// Send a new Guid for these connections and the number of associated channels
					Message.Negotiation.New n = new Message.Negotiation.New
					{
						guid = Guid.NewGuid(),
						nbChannels = (ushort) Math.Min(channels != null ? channels.Count : 0, ushort.MaxValue)
					};

					connection.Send(n);

					if(n.nbChannels <= 0)
					{
						Drop(connection, "No channels configured.");
					}

					for(ushort i = 0; i < n.nbChannels; i++)
					{
						ServerChannel channel = channels[i];

						Message.Negotiation.Parameters param = new Message.Negotiation.Parameters
						{
							guid = n.guid,
							channel = i,
							type = channel.type,
							heartbeat = channel.Heartbeat,
							autoReconnect = !channel.disableAutoReconnect
						};

						connection.Send(param);
					}

					if(monitor == null)
					{
						monitor = connection;

						monitorThread.Start();
					}
				}
				else if(msg.IsType<Message.Negotiation.Channel.TCP>())
				{
					Message.Negotiation.Channel.TCP tcp = (Message.Negotiation.Channel.TCP) msg;

					connection.SetConfig(tcp.guid, tcp.channel, channels[tcp.channel].Heartbeat);

					SaveChannel(connection);
				}
				else
				{
					Drop(connection, "Unsupported negotiation command '{0}'.", msg.GetType());
				}
			}
			else
			{
				Drop(connection, "Expected to receive some negotiation command.");
			}
        }

		protected void MonitorWorker()
		{
			while(!stopEvent.WaitOne(0))
			{
				Message.Base msg;

				if(monitor.Receive(out msg))
				{
					if(msg.IsType<Message.Negotiation.Channel.UDP>())
					{
						Message.Negotiation.Channel.UDP response = (Message.Negotiation.Channel.UDP) msg;

						ServerChannel channel = channels[response.channel];

						Message.Negotiation.Parameters param = new Message.Negotiation.Parameters
						{
							guid = response.guid,
							channel = response.channel,
							type = channel.type,
							heartbeat = channel.Heartbeat,
							autoReconnect = !channel.disableAutoReconnect
						};

						UdpConnectionParams udp_param = SendUdpParams(monitor, param);

						ConnectUdp(udp_param, response);
					}
				}
			}
		}
        #endregion
    }
}

#endif // !NETFX_CORE
