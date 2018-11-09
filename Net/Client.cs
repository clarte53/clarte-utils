using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.Net
{
    public class Client : Base
    {
        #region Members
        public string hostname = "localhost";
        public uint port;
        public Credentials credentials;
        public List<uint> openPorts;
        public List<Channel> channels;
        #endregion
    }
}
