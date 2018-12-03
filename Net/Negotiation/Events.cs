using System;
using System.Net;
using UnityEngine.Events;

namespace CLARTE.Net.Negotiation
{
    public static class Events
    {
        [Serializable]
        public class ConnectionCallback : UnityEvent<IPAddress, Guid, ushort>
        {

        }

        [Serializable]
        public class DisconnectionCallback : UnityEvent<IPAddress, Guid, ushort>
        {

        }

        [Serializable]
        public class ReceiveCallback : UnityEvent<IPAddress, Guid, ushort, byte[]>
        {

        }
    }
}
