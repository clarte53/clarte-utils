using UnityEngine;
using UnityEngine.XR;

namespace CLARTE.Input
{
    public class NamedTracker : Tracker
    {
        #region Members
        public string name;
        #endregion

        #region Tracker implementation
        protected override bool IsNode(XRNodeState node)
        {
            return InputTracking.GetNodeName(node.uniqueID).Trim().ToUpper() == name.Trim().ToUpper();
        }

        protected override bool IsSameNode(XRNodeState node) {
            return InputTracking.GetNodeName(node.uniqueID).Trim().ToUpper() == name.Trim().ToUpper();
        }

        protected override void OnNodeAdded(XRNodeState node)
        {
            Debug.LogFormat("Named tracker '{0}' is associated to object '{1}'", InputTracking.GetNodeName(uniqueID), gameObject.name);
        }

        protected override void OnNodeRemoved()
        {
            Debug.LogFormat("Named tracker '{0}' is removed from object '{1}'", InputTracking.GetNodeName(uniqueID), gameObject.name);
        }
        #endregion
    }
}
