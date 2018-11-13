using System;

namespace CLARTE.Net.Negotiation
{
    [Serializable]
    public class Channel
    {
        public enum Type : ushort
        {
            TCP,
            UDP,
        }

        #region Members
        public Events.ReceiveCallback onReceive;
        protected Connection.Base connection;
        #endregion

        #region Public methods
        public void Close()
        {
            if(connection != null)
            {
                connection.Close();
            }
        }

        public void Send(byte[] data)
        {

        }

        public void SetConnection(Connection.Base c)
        {
            connection = c;
        }
        #endregion
    }

    [Serializable]
    public class ServerChannel : Channel
    {
        #region Members
        public Type type;
        public bool encrypted;
        public bool signed;
        #endregion
    }
}
