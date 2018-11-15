using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

namespace CLARTE.Net.Negotiation
{
    public class Client : Base<Channel>
    {
        #region Members
        public const uint maxSupportedVersion = 1;

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

        #region MonoBehaviour callbacks
        protected override void Awake()
        {
            base.Awake();

            Connect(); // For debug purposes
        }
        #endregion

        #region Public methods
        public void Connect()
        {
            if(state == State.STARTED)
            {
                Debug.LogFormat("Start connection to {0}:{1}", hostname, port);

                state = State.INITIALIZING;

                ConnectTcp(-1);
            }
            else
            {
                Debug.LogErrorFormat("Invalid connection attempt to server when in state {0}.", state);
            }
        }
        #endregion

        #region Connection methods
        protected void ConnectTcp(int channel)
        {
            // Create a new TCP client
            Connection.TcpWithChannel connection = new Connection.TcpWithChannel(new TcpClient(AddressFamily.InterNetworkV6), channel);

            lock(initializedConnections)
            {
                initializedConnections.Add(connection);
            }

            // Start asynchronous connection to server
            connection.initialization = tasks.Add(() => connection.client.BeginConnect(hostname, (int) port, Connected, connection));
        }

        protected void Connected(IAsyncResult async_result)
        {
            try
            {
                // Finalize connection to server
                Connection.TcpWithChannel connection = (Connection.TcpWithChannel) async_result.AsyncState;

                connection.client.EndConnect(async_result);

                // We should be connected
                if(connection.client.Connected)
                {
                    Debug.LogFormat("Connected to {0}:{1}", hostname, port);

                    // Get the stream associated with this connection
                    connection.stream = connection.client.GetStream();

                    // Get the protocol version
                    if(Receive(connection, out connection.version))
                    {
                        if(connection.version > maxSupportedVersion)
                        {
                            Debug.LogWarningFormat("Usupported protocol version '{0}'. Using version '{1}' instead.", connection.version, maxSupportedVersion);

                            connection.version = maxSupportedVersion;
                        }

                        // Send the agreed protocol version
                        Send(connection, connection.version);

                        bool encrypted;

                        // Check if we must wrap the stream in an encrypted SSL channel
                        if(Receive(connection, out encrypted))
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
                Connection.TcpWithChannel connection = (Connection.TcpWithChannel) async_result.AsyncState;

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

        protected void ValidateCredentials(Connection.TcpWithChannel connection)
        {
            Send(connection, credentials.username);
            Send(connection, credentials.password);

            bool credentials_ok;

            // Check if the sent credentials are OK
            if(Receive(connection, out credentials_ok))
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

        protected void NegotiateChannels(Connection.TcpWithChannel connection)
        {
            // Send channel negotiation flag
            Send(connection, connection.channel < 0);

            // Check if we must negotiate other channel or just open the current one
            if(connection.channel < 0)
            {
                // Receive the channels descriptions
                ushort nb_channels;

                if(Receive(connection, out nb_channels))
                {
                    if(nb_channels > 0)
                    {
                        for(ushort i = 0; i < nb_channels; i++)
                        {
                            ushort raw_channel_type;

                            if(Receive(connection, out raw_channel_type))
                            {
                                switch((Channel.Type) raw_channel_type)
                                {
                                    case Channel.Type.TCP:
                                        ConnectTcp(i);
                                        break;
                                    case Channel.Type.UDP:
                                        ushort channel = i; // To avoid weird behaviour of lambda catching base types as reference in loops

                                        ConnectUdp(connection, channel);
                                        break;
                                }
                            }
                            else
                            {
                                Drop(connection, "Expected to receive channel type.");
                            }
                        }
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
                if(connection.channel >= 0)
                {
                    Send(connection, (ushort) connection.channel);

                    SaveChannel(connection, (ushort) connection.channel);
                }
                else
                {
                    Drop(connection, "Invalid channel index.");
                }
            }
        }
        #endregion

        #region Internal methods
        protected bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            switch(sslPolicyErrors)
            {
                case SslPolicyErrors.None:
                    return true;
                case SslPolicyErrors.RemoteCertificateNameMismatch:
                    Debug.LogWarningFormat("The name of the certificate does not match the hostname. Certificate = '{0}', hostname = '{1}'.", certificate.Subject, hostname);

                    return true;
                case SslPolicyErrors.RemoteCertificateChainErrors:
                    foreach(X509ChainStatus chainStatus in chain.ChainStatus)
                    {
                        if(chainStatus.Status != X509ChainStatusFlags.NoError && chainStatus.Status != X509ChainStatusFlags.UntrustedRoot)
                        {
                            return false;
                        }
                    }

                    Debug.LogWarning("The root certificate is untrusted.");

                    return true;
                default:
                    return false;
            }
        }
        #endregion
    }
}
