#if !NETFX_CORE

using System;
using UnityEngine;

namespace CLARTE.Net.Negotiation
{
    [Serializable]
    public abstract class Channel
    {
        public enum Type : ushort
        {
            TCP,
            UDP,
        }

        #region Members
        public Events.ConnectionCallback onConnected;
        public Events.DisconnectionCallback onDisconnected;
		public Events.ExceptionCallback onException;
		public Events.ReceiveCallback onReceive;
        public Events.ReceiveProgressCallback onReceiveProgress;
        #endregion
    }

    [Serializable]
    public class ServerChannel : Channel
	{
        #region Members
        public Type type;
        [Range(0.1f, 300f)]
        public float heartbeat; // In seconds
		public bool disableHeartbeat;
		#endregion
	}

	[Serializable]
	public class ClientChannel : Channel
	{
		#region Members
		public bool disableAutoReconnect;
		#endregion
	}
}

#endif // !NETFX_CORE
