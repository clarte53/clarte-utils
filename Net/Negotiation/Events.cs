using System;
using System.Net;
using UnityEngine.Events;

namespace CLARTE.Net.Negotiation
{
    public static class Events
    {
        [Serializable]
        public class ConnectionCallback : UnityEvent<IPAddress, ushort>
        {

        }

        [Serializable]
        public class DisconnectionCallback : UnityEvent<IPAddress, ushort>
        {

        }

        [Serializable]
        public class ReceiveCallback : UnityEvent<IPAddress, ushort, byte[]>
        {

        }
    }
}
