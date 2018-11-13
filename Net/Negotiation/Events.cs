using System;
using UnityEngine.Events;

namespace CLARTE.Net.Negotiation
{
    public static class Events
    {
        [Serializable]
        public class ReceiveCallback : UnityEvent<byte[]>
        {

        }
    }
}
