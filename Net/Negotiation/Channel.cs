﻿using System;

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
