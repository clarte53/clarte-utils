using System;
using System.Collections.Generic;
using UnityEngine;

namespace CLARTE.DataStructures
{
    public class Octree<T>
    {
        public struct Cell
        {
            public Vector3 center;
            public T value;

            #region Constructors
            public Cell(Vector3 c, T val)
            {
                center = c;
                value = val;
            }
            #endregion
        }

        public struct UpdateResult
        {
            #region Members
            public bool keepValue;
            public T value;
            #endregion

            #region Constructors
            public UpdateResult(bool keep, T val)
            {
                keepValue = keep;
                value = val;
            }
            #endregion
        }
        /*
        public static class Utils
        {
            public static T AverageAggregator(IEnumerator<T> values, Func<T, T, T> sum, Func<T, uint, T> div)
            {
                T avg = default(T);

                uint count = 0;

                while (values.MoveNext())
                {
                    avg = sum(avg, values.Current);
                    count++;
                }

                return div(avg, count);
            }
        }
        */
        protected abstract class NodeBase
        {
            #region Abstract methods
            public abstract void Reset(Action<NodeBase> release);
            #endregion
        }

        protected class Node : NodeBase
        {
            #region Members
            public const int dim = 8;

            public NodeBase[] children;
            #endregion

            #region Constructors
            public Node()
            {
                children = new NodeBase[dim];
            }
            #endregion

            #region Public methods
            public override void Reset(Action<NodeBase> release)
            {
                for(int i = 0; i < dim; i++)
                {
                    release(children[i]);

                    children[i] = null;
                }
            }
            /*
            public IEnumerator<T> GetChildrenValues()
            {
                foreach(NodeBase child in children)
                {
                    switch(child)
                    {
                        case Leaf leaf:
                            yield return leaf.value;

                            break;

                        case Node node:
                            IEnumerator<T> it = node.GetChildrenValues();

                            while (it.MoveNext())
                            {
                                yield return it.Current;
                            }

                            break;
                    }
                }
            }
            */
            #endregion
        }

        protected class Leaf : NodeBase
        {
            #region Members
            public T value;
            #endregion

            #region Constructors
            public Leaf()
            {
                value = default(T);
            }
            #endregion

            #region Public methods
            public override void Reset(Action<NodeBase> release)
            {
                value = default(T);
            }
            #endregion
        }

        #region Members
        protected readonly float half_resolution;
        protected readonly int maxDepth;
        protected readonly Bounds bounds;
        protected NodeBase root;
        protected Stack<NodeBase> nodePool;
        protected Stack<NodeBase> leafPool;
        //protected Func<IEnumerator<T>, T> aggregator;
        protected T initValue;
        #endregion

        #region Constructors
        public Octree(Bounds bounds, float resolution, /*Func<IEnumerator<T>, T> aggregator,*/ T init_value = default(T))
        {
            //this.aggregator = aggregator;

            half_resolution = 0.5f * resolution;
            initValue = init_value;

            nodePool = new Stack<NodeBase>();
            leafPool = new Stack<NodeBase>();

            root = null;

            float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

            maxDepth = Mathf.CeilToInt((float) Math.Log(size / resolution, 2));

            size = Mathf.Pow(2, maxDepth) * resolution;

            this.bounds = new Bounds(bounds.center, size * Vector3.one);

            if (maxDepth >= 32)
            {
                maxDepth = 0;

                throw new ArgumentOutOfRangeException("The bounds is too large or the resolution too small. It would result in a tree with a depth >= 32, which is not supported.", "bounds + resolution");
            }
        }
        #endregion

        #region Getters / Setters
        #endregion

        #region Public methods
        public IEnumerator<Cell> Get()
        {
            return Get(root, maxDepth, bounds.center);
        }

        public void Update(Vector3 position, Func<T, UpdateResult> updater)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            if(position.x < min.x || position.y < min.y || position.z < min.z || position.x >= max.x || position.y >= max.y || position.z >= max.z)
            {
                throw new ArgumentOutOfRangeException("The given positon is outside the range of the octree.", "position");
            }

            root = Update(root, maxDepth, bounds.center, position, updater);
        }
        #endregion

        #region Internal methods
        protected U GetNew<U>() where U : NodeBase, new()
        {
            Stack<NodeBase> pool = typeof(Leaf).IsAssignableFrom(typeof(U)) ? leafPool : nodePool;

            if(pool.Count > 0)
            {
                return pool.Pop() as U;
            }
            else
            {
                return new U();
            }
        }

        protected void Release(NodeBase node)
        {
            if (node != null)
            {
                Stack<NodeBase> pool = typeof(Leaf).IsAssignableFrom(node.GetType()) ? leafPool : nodePool;

                node.Reset(Release);

                pool.Push(node);
            }
        }

        protected IEnumerator<Cell> Get(NodeBase node, int depth, Vector3 center)
        {
            if(node != null)
            {
                switch(node)
                {
                    case Leaf leaf:
                        yield return new Cell(center, leaf.value);

                        break;

                    case Node n:
                        for(int i = 0; i < Node.dim; i++)
                        {
                            int x = ((i & 1) << 1) - 1;
                            int y = (i & 2) - 1;
                            int z = ((i & 4) >> 1) - 1;

                            int d = depth - 1;

                            IEnumerator<Cell> it = Get(n.children[i], d, center + half_resolution * (1 << d) * new Vector3(x, y, z));

                            while(it.MoveNext())
                            {
                                yield return it.Current;
                            }
                        }

                        break;
                }
            }
        }

        protected NodeBase Update(NodeBase node, int depth, Vector3 center, Vector3 position, Func<T, UpdateResult> updater)
        {
            /*
            Vector3 min = center - half_resolution * Vector3.one;
            Vector3 max = center + half_resolution * Vector3.one;

            bool is_leaf = 
                position.x >= min.x &&
                position.y >= min.y &&
                position.z >= min.z &&
                position.x < max.x &&
                position.y < max.y &&
                position.z < max.z;
            */
            bool is_leaf = depth == 0;

            if (node == null)
            {
                if(is_leaf)
                {
                    Leaf leaf = GetNew<Leaf>();

                    leaf.value = initValue;

                    node = leaf;
                }
                else
                {
                    node = GetNew<Node>();
                }
            }

            if (is_leaf)
            {
                switch(node)
                {
                    case Node n:
                        // This should only happens when resolution is decreased.
                        throw new NotSupportedException("Invalid octree node where a leaf was expected.");
                        /*
                        T value = aggregator(n.GetChildrenValues());

                        Release(n);

                        node = GetNew<Leaf>();

                        ((Leaf) node).value = value;

                        break;
                        */
                }

                Leaf leaf = node as Leaf;

                UpdateResult r = updater(leaf.value);

                leaf.value = r.value;

                if(!r.keepValue)
                {
                    Release(leaf);

                    node = null;
                }
            }
            else
            {
                switch (node)
                {
                    case Leaf leaf:
                        // This should only happens when resolution is increased.
                        throw new NotSupportedException("Invalid octree leaf where a node was expected.");
                        /*
                        T value = leaf.value;

                        Release(leaf);

                        node = GetNew<Node>();

                        node = Update(node, depth, center, center, v => new UpdateResult(true, value));

                        break;
                        */
                }

                Node n = node as Node;

                int x = position.x >= center.x ? 1 : 0;
                int y = position.y >= center.y ? 2 : 0;
                int z = position.z >= center.z ? 4 : 0;

                int index = x | y | z;

                int d = depth - 1;

                Vector3 c = center + half_resolution * (1 << d) * new Vector3((x << 1) - 1, y - 1, (z >> 1) - 1);

                n.children[index] = Update(n.children[index], d, c, position, updater);

                bool prune = true;

                foreach(NodeBase child in n.children)
                {
                    if(child != null)
                    {
                        prune = false;

                        break;
                    }
                }

                if(prune)
                {
                    Release(n);

                    node = null;
                }
            }

            return node;
        }
        #endregion
    }
}
