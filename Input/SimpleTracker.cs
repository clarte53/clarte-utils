using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CLARTE.Input
{
    public class SimpleTracker : Tracker
    {
        #region Members
        protected static Dictionary<ulong, ushort> trackedIds = new Dictionary<ulong, ushort>();

        public XRNode type;
        #endregion

        #region Tracker implementation
        protected override bool IsNode(XRNodeState node)
        {
            return (node.nodeType == type && !(trackedIds.ContainsKey(node.uniqueID) && trackedIds[node.uniqueID] > 0));
        }

        protected override bool IsSameNode(XRNodeState node) {
            return node.nodeType == type && trackedIds.ContainsKey(node.uniqueID) && trackedIds[node.uniqueID] > 0;
        }

        protected override void OnNodeAdded(XRNodeState node)
        {
            if(!trackedIds.ContainsKey(node.uniqueID))
            {
                trackedIds.Add(uniqueID, 0);
            }

            trackedIds[uniqueID]++;

            Debug.LogFormat("Tracker '{0}' of type '{1}' is associated to object '{2}'", uniqueID, type, gameObject.name);
        }

        protected override void OnNodeRemoved()
        {
            Debug.LogFormat("Tracker '{0}' of type '{1}' is removed from object '{2}'", uniqueID, type, gameObject.name);

            trackedIds[uniqueID]--;
        }
        #endregion
    }
}
