using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace CLARTE.Net
{
    public class Client : Base
    {
        #region Members
        public const uint maxSupportedVersion = 1;

        public string hostname = "localhost";
        public uint port;
        public Credentials credentials;
        public List<uint> openPorts;
        public List<Channel> channels;
        #endregion

        #region MonoBehaviour callbacks
        protected void Awake()
        {
            Connect(); // For debug purposes
        }
        #endregion

        #region Public methods
        public void Connect()
        {
            UnityEngine.Debug.LogFormat("Start connection to {0}:{1}", hostname, port);

            // Create a new TCP client
            Connection connection = new Connection(new TcpClient(AddressFamily.InterNetworkV6));

            // Start asynchronous connection to server
            connection.client.BeginConnect(hostname, (int) port, Connected, connection);
        }
        #endregion

        #region Connection methods
        protected void Connected(IAsyncResult async_result)
        {
            try
            {
                // Finalize connection to server
                Connection connection = (Connection) async_result.AsyncState;

                connection.client.EndConnect(async_result);

                // We should be connected
                if(connection.client.Connected)
                {
                    UnityEngine.Debug.LogFormat("Connected to {0}:{1}", hostname, port);

                    // Get the stream associated with this connection
                    connection.stream = connection.client.GetStream();

                    // Get the protocol version
                    if(Receive(connection, out connection.version))
                    {
                        if(connection.version > maxSupportedVersion)
                        {
                            UnityEngine.Debug.LogWarningFormat("Usupported protocol version '{0}'. Using version '{1}' instead.", connection.version, maxSupportedVersion);

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
                            UnityEngine.Debug.LogError("Expected to receive encryption status. Dropping connection.");

                            Close(connection);

                            return;
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogError("Expected to receive protocol version. Dropping connection.");

                        Close(connection);

                        return;
                    }
                }
                else
                {
                    UnityEngine.Debug.LogErrorFormat("The connection to {0}:{1} failed.", hostname, port);

                    Close(connection);

                    return;
                }
            }
            catch(Exception exception)
            {
                UnityEngine.Debug.LogErrorFormat("{0}: {1}\n{2}", exception.GetType(), exception.Message, exception.StackTrace);
            }
        }

        protected void Authenticated(IAsyncResult async_result)
        {
            try
            {
                // Finalize the authentication as client for the SSL stream
                Connection connection = (Connection) async_result.AsyncState;

                ((SslStream) connection.stream).EndAuthenticateAsClient(async_result);

                ValidateCredentials(connection);
            }
            catch(Exception)
            {
                UnityEngine.Debug.LogError("Authentication failed");
            }
        }

        protected void ValidateCredentials(Connection connection)
        {
            Send(connection, credentials.username);
            Send(connection, credentials.password);

            bool credentials_ok;

            if(Receive(connection, out credentials_ok))
            {
                if(credentials_ok)
                {
                    //TODO
                    UnityEngine.Debug.Log("Success");
                }
                else
                {
                    UnityEngine.Debug.LogError("Invalid credentials. Dropping connection.");

                    Close(connection);

                    return;
                }
            }
            else
            {
                UnityEngine.Debug.LogError("Expected to receive credentials validation. Dropping connection.");

                Close(connection);

                return;
            }
        }

        protected void Close(Connection connection)
        {
            SafeDispose(connection.stream);
            SafeDispose(connection.client);
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
                    UnityEngine.Debug.LogWarningFormat("The name of the certificate does not match the hostname. Certificate = '{0}', hostname = '{1}'.", certificate.Subject, hostname);

                    return true;
                case SslPolicyErrors.RemoteCertificateChainErrors:
                    foreach(X509ChainStatus chainStatus in chain.ChainStatus)
                    {
                        if(chainStatus.Status != X509ChainStatusFlags.NoError && chainStatus.Status != X509ChainStatusFlags.UntrustedRoot)
                        {
                            return false;
                        }
                    }

                    UnityEngine.Debug.LogWarning("The root certificate is untrusted.");

                    return true;
                default:
                    return false;
            }
        }
        #endregion
    }
}
