using System;

namespace CLARTE.Net
{
    public enum StreamType
    {
        TCP,
        UDP,
    }

    [Serializable]
    public class Channel
    {
        #region Members
        public Events.ReceiveCallback onReceive;
        #endregion

        #region Public methods
        public void Send(byte[] data)
        {

        }
        #endregion
    }

    [Serializable]
    public class ServerChannel : Channel
    {
        #region Members
        public StreamType type;
        public bool encrypted;
        public bool signed;
        #endregion
    }
}
