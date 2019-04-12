#if !NETFX_CORE

using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

namespace CLARTE.Net.Negotiation
{
    public class Client : Base<ClientChannel>
    {
        [Serializable]
        public class CertificateValidation
        {
            public bool allowSelfSigned = false;
            public bool allowInvalidHostname = false;
        }

        #region Members
        public const uint maxSupportedVersion = 1;

        public CertificateValidation certificateValidation;
        public string hostname = "localhost";
        public uint port;
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

                    CloseInitializedConnections();

                    CloseOpenedChannels();
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
			if(state == State.RUNNING && connection != null && connection.AutoReconnect)
			{
				Type type = connection.GetType();

				if(typeof(Connection.Tcp).IsAssignableFrom(type))
				{
					ConnectTcp(connection.Remote, connection.Channel, connection.Heartbeat);
				}
				else if(typeof(Connection.Udp).IsAssignableFrom(type))
				{
					Connection.Udp c = (Connection.Udp) connection;

					UdpClient udp = new UdpClient(c.LocalPort, AddressFamily.InterNetwork);

					SaveChannel(new Connection.Udp(this, udp, connection.Remote, connection.Channel, connection.Heartbeat, connection.AutoReconnect, DisconnectionHandler, connection.Address, c.LocalPort, c.RemotePort));
				}
			}
		}
		#endregion

		#region Public methods
		public void Connect()
        {
            if(state == State.STARTED)
            {
                Debug.LogFormat("Start connection to {0}:{1}", hostname, port);

                state = State.INITIALIZING;

                ConnectTcp(Guid.Empty, 0, defaultHeartbeat);
            }
            else
            {
                Debug.LogErrorFormat("Invalid connection attempt to server when in state {0}.", state);
            }
        }
        #endregion

        #region Connection methods
        protected void ConnectTcp(Guid remote, ushort channel, TimeSpan heartbeat)
        {
			bool auto_reconnect = (channels != null && channels.Count > channel ? !channels[channel].disableAutoReconnect : true);

            // Create a new TCP client
            Connection.Tcp connection = new Connection.Tcp(new TcpClient(), remote, channel, heartbeat, auto_reconnect, DisconnectionHandler);

            lock(initializedConnections)
            {
                initializedConnections.Add(connection);
            }

            // Start asynchronous connection to server
            connection.initialization = Threads.Tasks.Add(() => connection.client.BeginConnect(hostname, (int) port, Connected, connection));
        }

        protected void Connected(IAsyncResult async_result)
        {
            try
            {
                // Finalize connection to server
                Connection.Tcp connection = (Connection.Tcp) async_result.AsyncState;

                connection.client.EndConnect(async_result);

                // We should be connected
                if(connection.client.Connected)
                {
                    Debug.LogFormat("Connected to {0}:{1}", hostname, port);

                    // Get the stream associated with this connection
                    connection.stream = connection.client.GetStream();

                    // Get the protocol version
                    if(connection.Receive(out connection.version))
                    {
                        if(connection.version > maxSupportedVersion)
                        {
                            Debug.LogWarningFormat("Usupported protocol version '{0}'. Using version '{1}' instead.", connection.version, maxSupportedVersion);

                            connection.version = maxSupportedVersion;
                        }

                        // Send the agreed protocol version
                        connection.Send(connection.version);

                        bool encrypted;

                        // Check if we must wrap the stream in an encrypted SSL channel
                        if(connection.Receive(out encrypted))
                        {
                            if(encrypted)
                            {
                                // Create the SSL wraping stream
                                connection.stream = new SslStream(connection.stream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate));

                                // Authenticate with the server
                                ((SslStream) connection.stream).BeginAuthenticateAsClient(hostname, Authenticated, connection);
                            }
                            else
                            {
                                // No encryption, the channel stay as is
                                ValidateCredentials(connection);
                            }
                        }
                        else
                        {
                            Drop(connection, "Expected to receive encryption status.");
                        }
                    }
                    else
                    {
                        Drop(connection, "Expected to receive protocol version.");
                    }
                }
                else
                {
                    Drop(connection, "The connection to {0}:{1} failed.", hostname, port);
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
            try
            {
                // Finalize the authentication as client for the SSL stream
                Connection.Tcp connection = (Connection.Tcp) async_result.AsyncState;

                ((SslStream) connection.stream).EndAuthenticateAsClient(async_result);

                ValidateCredentials(connection);
            }
            catch(DropException)
            {
                throw;
            }
            catch(Exception)
            {
                Debug.LogError("Authentication failed");
            }
        }

        protected void ValidateCredentials(Connection.Tcp connection)
        {
            connection.Send(credentials.username);
            connection.Send(credentials.password);

            bool credentials_ok;

            // Check if the sent credentials are OK
            if(connection.Receive(out credentials_ok))
            {
                if(credentials_ok)
                {
                    NegotiateChannels(connection);
                }
                else
                {
                    Drop(connection, "Invalid credentials.");
                }
            }
            else
            {
                Drop(connection, "Expected to receive credentials validation.");
            }
        }

        protected void NegotiateChannels(Connection.Tcp connection)
        {
            // Send channel negotiation flag
            connection.Send(connection.Remote == Guid.Empty);

            // Check if we must negotiate other channel or just open the current one
            if(connection.Remote == Guid.Empty)
            {
                // Receive the connection identifier
                byte[] raw_remote;

                if(connection.Receive(out raw_remote))
                {
                    Guid remote = new Guid(raw_remote);

                    // Receive the channels descriptions
                    ushort nb_channels;

                    if(connection.Receive(out nb_channels))
                    {
                        if(nb_channels > 0)
                        {
                            for(ushort i = 0; i < nb_channels; i++)
                            {
                                ushort raw_channel_type;
                                ushort raw_heartbeat;

                                if(connection.Receive(out raw_channel_type))
                                {
                                    if(connection.Receive(out raw_heartbeat))
                                    {
                                        TimeSpan heartbeat = new TimeSpan(raw_heartbeat * 100 * TimeSpan.TicksPerMillisecond);

                                        switch((Channel.Type) raw_channel_type)
                                        {
                                            case Channel.Type.TCP:
                                                ConnectTcp(remote, i, heartbeat);
                                                break;
                                            case Channel.Type.UDP:
												bool auto_reconnect = !channels[i].disableAutoReconnect;

												connection.Send(auto_reconnect);

                                                ConnectUdp(connection, remote, i, heartbeat, auto_reconnect);
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        Drop(connection, "Expected to receive channel heartbeat for channel {0}.", i);
                                    }
                                }
                                else
                                {
                                    Drop(connection, "Expected to receive channel type for channel {0}.", i);
                                }
                            }

                            state = State.RUNNING;
                        }
                        else
                        {
                            Drop(connection, "No channels configured.");
                        }
                    }
                    else
                    {
                        Drop(connection, "Expected to receive the number of channels.");

                    }
                }
                else
                {
                    Drop(connection, "Expected to receive the connection identifier.");

                }
            }
            else
            {
                connection.Send(connection.Remote.ToByteArray());
                connection.Send(connection.Channel);

                SaveChannel(connection);
            }
        }
        #endregion

        #region Internal methods
        protected bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            SslPolicyErrors handled = SslPolicyErrors.None;

            if(sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            if((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
            {
                if(certificateValidation.allowInvalidHostname)
                {
                    Debug.LogWarningFormat("The name of the certificate does not match the hostname. Certificate = '{0}', hostname = '{1}'.", certificate.Subject, hostname);
                }
                else
                {
                    return false;
                }

                handled |= SslPolicyErrors.RemoteCertificateNameMismatch;
            }

            if((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                if(certificateValidation.allowSelfSigned)
                {
                    foreach(X509ChainStatus chainStatus in chain.ChainStatus)
                    {
                        if(chainStatus.Status != X509ChainStatusFlags.NoError && chainStatus.Status != X509ChainStatusFlags.UntrustedRoot)
                        {
                            return false;
                        }
                    }

                    Debug.LogWarning("The root certificate is untrusted.");
                }
                else
                {
                    return false;
                }

                handled |= SslPolicyErrors.RemoteCertificateChainErrors;
            }

            if((sslPolicyErrors & handled) == handled)
            {
                return true;
            }

            return false;
        }
        #endregion
    }
}

#endif // !NETFX_CORE
