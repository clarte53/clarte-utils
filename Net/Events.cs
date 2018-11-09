using System;
using UnityEngine.Events;

namespace CLARTE.Net
{
    public static class Events
    {
        [Serializable]
        public class ReceiveCallback : UnityEvent<byte[]>
        {

        }
    }
}
