﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CLARTE.Input
{
    public abstract class Tracker : MonoBehaviour
    {
        #region Members
        protected List<XRNodeState> nodes;
        protected XRNode currentType;
        protected ulong uniqueID;
        protected bool tracked;
        #endregion

        #region Abstract methods
        protected abstract bool IsNode(XRNodeState node);
        protected abstract bool IsSameNode(XRNodeState node);
        protected abstract void OnNodeAdded(XRNodeState node);
        protected abstract void OnNodeRemoved();
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
        protected virtual void Awake()
        {
            nodes = new List<XRNodeState>();

            uniqueID = 0;

            Tracked = false;
        }

        protected virtual void OnDisable()
        {
            RemoveNode();
        }

        protected virtual void Update()
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
                    found = (node.nodeType == currentType) && IsSameNode(node);

                    break;
                }
            }

            return found;
        }

        protected void SearchValidNode(List<XRNodeState> nodes)
        {
            foreach(XRNodeState node in nodes)
            {
                if(uniqueID == 0 && IsNode(node))
                {
                    uniqueID = node.uniqueID;
                    currentType = node.nodeType;

                    Tracked = node.tracked;

                    OnNodeAdded(node);

                    break;
                }
            }
        }

        protected void RemoveNode()
        {
            if(uniqueID != 0)
            {
                OnNodeRemoved();

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
