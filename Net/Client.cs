using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;

namespace CLARTE.Net
{
    public class Client : Base
    {
        #region Members
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
            Connection connection = new Connection(new TcpClient());

            // Start asynchronous connection to server
            connection.client.BeginConnect(hostname, (int) port, Connected, connection);
        }
        #endregion

        #region Connection methods
        protected void Connected(IAsyncResult async_result)
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

                bool encrypted;

                // Check if we must wrap the stream in an encrypted SSL channel
                if(Receive(connection.stream, out encrypted))
                {
                    if(encrypted)
                    {
                        // Create the SSL wraping stream
                        connection.stream = new SslStream(connection.stream);

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
                UnityEngine.Debug.LogErrorFormat("The connection to {0}:{1} failed.", hostname, port);

                Close(connection);

                return;
            }
        }

        protected void Authenticated(IAsyncResult async_result)
        {
            // Finalize the authentication as client for the SSL stream
            Connection connection = (Connection) async_result.AsyncState;

            ((SslStream) connection.stream).EndAuthenticateAsClient(async_result);

            ValidateCredentials(connection);
        }

        protected void ValidateCredentials(Connection connection)
        {
            Send(connection.stream, credentials.username);
            Send(connection.stream, credentials.password);

            bool credentials_ok;

            if(Receive(connection.stream, out credentials_ok))
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
    }
}
