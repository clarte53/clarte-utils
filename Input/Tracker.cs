using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CLARTE.Input
{
    public class Tracker : MonoBehaviour
    {
        #region Members
        public XRNode type;
        public ulong id;

        protected static Dictionary<ulong, ushort> trackedIds = new Dictionary<ulong, ushort>();

        protected List<XRNodeState> nodes;
        protected XRNode currentType;
        protected ulong uniqueID;
        protected bool tracked;
        #endregion

        #region Getter / Setter
        public bool Tracked
        {
            get
            {
                return tracked;
            }

            protected set
            {
                tracked = value;

                EnableComponents(tracked);
            }
        }
        #endregion

        #region MonoBehaviour callbacks
        protected void Awake()
        {
            nodes = new List<XRNodeState>();

            uniqueID = 0;

            Tracked = false;
        }

        protected void OnDisable()
        {
            RemoveNode();
        }

        protected void Update()
        {
            InputTracking.GetNodeStates(nodes);

            if(uniqueID == 0)
            {
                SearchValidNode(nodes);
            }
            else if(!CheckConnectedNode(nodes))
            {
                RemoveNode();
            }
  
            if(uniqueID != 0)
            {
                foreach(XRNodeState node in nodes)
                {
                    if(node.uniqueID == uniqueID)
                    {
                        if(node.tracked != Tracked)
                        {
                            Tracked = node.tracked;
                        }

                        if(Tracked)
                        {
                            Vector3 pos;
                            Quaternion rot;

                            if(node.TryGetPosition(out pos))
                            {
                                transform.position = pos;
                            }

                            if(node.TryGetRotation(out rot))
                            {
                                transform.rotation = rot;
                            }
                        }

                        break;
                    }
                }
            }
        }
        #endregion

        #region Helper methods
        protected bool CheckConnectedNode(List<XRNodeState> nodes)
        {
            bool found = false;

            foreach(XRNodeState node in nodes)
            {
                if(node.uniqueID == uniqueID)
                {
                    found = (node.nodeType == currentType);

                    break;
                }
            }

            return found;
        }

        protected void SearchValidNode(List<XRNodeState> nodes)
        {
            foreach(XRNodeState node in nodes)
            {
                if(uniqueID == 0 && (node.uniqueID == id || (id == 0 && node.nodeType == type && !(trackedIds.ContainsKey(node.uniqueID) && trackedIds[node.uniqueID] > 0))))
                {
                    uniqueID = node.uniqueID;
                    currentType = node.nodeType;

                    Tracked = node.tracked;

                    if(!trackedIds.ContainsKey(node.uniqueID))
                    {
                        trackedIds.Add(uniqueID, 0);
                    }

                    trackedIds[uniqueID]++;

                    Debug.LogFormat("Tracker '{0}' of type '{1}' is associated to object '{2}'", uniqueID, type, gameObject.name);

                    break;
                }
            }
        }

        protected void RemoveNode()
        {
            if(uniqueID != 0)
            {
                Debug.LogFormat("Tracker '{0}' of type '{1}' is removed from object '{2}'", uniqueID, type, gameObject.name);

                trackedIds[uniqueID]--;

                uniqueID = 0;

                SearchValidNode(nodes);
            }
        }

        protected void EnableComponents(bool enable)
        {
            foreach(Collider collider in GetComponents<Collider>())
            {
                collider.enabled = enable;
            }

            foreach(Behaviour component in GetComponents<Behaviour>())
            {
                if(component != this)
                {
                    component.enabled = enable;
                }
            }

            foreach(Transform child in transform)
            {
                child.gameObject.SetActive(enable);
            }
        }
        #endregion
    }
}
