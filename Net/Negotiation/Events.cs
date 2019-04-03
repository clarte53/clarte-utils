﻿using System;
using System.Net;
using UnityEngine.Events;
using CLARTE.Serialization;

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

        [Serializable]
        public class ReceiveDeserializedCallback : UnityEvent<IPAddress, Guid, ushort, IBinarySerializable>
        {

        }

        [Serializable]
        public class ReceiveProgressCallback : UnityEvent<IPAddress, Guid, ushort, float>
        {

        }
    }
}