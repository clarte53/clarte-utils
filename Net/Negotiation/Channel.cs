#if !NETFX_CORE

using System;
using UnityEngine;
using UnityEngine.Events;

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
		public bool disableAutoReconnect;
		#endregion

		#region Public methods
		public TimeSpan Heartbeat
		{
			get
			{
				if(disableHeartbeat || heartbeat < 0.1f)
				{
					return new TimeSpan(0, 0, 0, 0, -1);
				}
				else
				{
					return new TimeSpan(((long) (heartbeat * 10)) * 100 * TimeSpan.TicksPerMillisecond);
				}
			}
		}
		#endregion
	}
}

#endif // !NETFX_CORE
