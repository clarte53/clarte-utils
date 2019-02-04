using UnityEngine;
using UnityEngine.XR;

namespace CLARTE.Input
{
    public class FixedTracker : Tracker
    {
        #region Members
        public ulong id;
        #endregion

        #region Tracker implementation
        protected override bool IsNode(XRNodeState node)
        {
            return (node.uniqueID == id);
        }

        protected override void OnNodeAdded(XRNodeState node)
        {
            Debug.LogFormat("Fixed tracker '{0}' is associated to object '{1}'", uniqueID, gameObject.name);
        }

        protected override void OnNodeRemoved()
        {
            Debug.LogFormat("Fixed tracker '{0}' is removed from object '{1}'", uniqueID, gameObject.name);
        }
        #endregion
    }
}
