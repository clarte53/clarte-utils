#if !NETFX_CORE

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using CLARTE.Serialization;

namespace CLARTE.Net.Negotiation
{
    [RequireComponent(typeof(Base))]
    public class BinarySerializerAdapter : MonoBehaviour
    {
        protected abstract class Context
        {
            #region Members
            public IEnumerator task;
            #endregion

            #region Abstract methods
            public abstract void Execute();
            #endregion
        }

        protected class Context<T> : Context
        {
            #region Members
            public Action<T> callback;
            public T data;
            #endregion

            #region Constructors
            public Context(Action<T> callback)
            {
                this.callback = callback;

                task = null;
                data = default(T);
            }
            #endregion

            #region Public methods
            public override void Execute()
            {
                callback(data);
            }
            #endregion
        }

        protected class SerializationContext : Context<byte[]>
        {
            #region Constructors
            public SerializationContext(Action<byte[]> callback) : base(callback)
            {

            }
            #endregion

            #region Public methods
            public void CopyData(byte[] buffer, uint size)
            {
                data = new byte[size];

                Array.Copy(buffer, data, size);
            }
            #endregion
        }

        protected class DeserializationContext : Context<IBinarySerializable>
        {
            #region Constructors
            public DeserializationContext(Action<IBinarySerializable> callback) : base(callback)
            {

            }
            #endregion

            #region Public methods
            public void SaveResult(object data)
            {
                this.data = (IBinarySerializable) data;
            }
            #endregion
        }

        #region Members
        public Events.ReceiveDeserializedCallback onReceive;

        protected Queue<SerializationContext> serializationTasks;
        protected Queue<DeserializationContext> deserializationTasks;
        protected SerializationContext currentSerialization;
        protected DeserializationContext currentDeserialization;
        protected Binary serializer;
        protected Base network;
        #endregion

        #region MonoBehaviour callbacks
        protected void Awake()
        {
            serializationTasks = new Queue<SerializationContext>();
            deserializationTasks = new Queue<DeserializationContext>();

            serializer = new Binary();

            network = GetComponent<Base>();

            currentSerialization = null;
            currentDeserialization = null;

            Action<IPAddress, Guid, ushort, byte[]> method = Receive;

            // Add this component as a receiver for receive events if necessary
            foreach(Channel channel in network.Channels)
            {
                bool found = false;

                int count = channel.onReceive.GetPersistentEventCount();

                for(int i = 0; i < count; i++)
                {
                    if(channel.onReceive.GetPersistentTarget(i) == this && channel.onReceive.GetPersistentMethodName(i) == method.Method.Name)
                    {
                        found = true;

                        break;
                    }
                }

                if(!found)
                {
                    channel.onReceive.AddListener(Receive);
                }
            }
        }

        protected void Update()
        {
            Update(serializationTasks, ref currentSerialization);
            Update(deserializationTasks, ref currentDeserialization);
        }
        #endregion

        #region Public methods
        public void Receive(IPAddress remote, Guid id, ushort channel, byte[] data)
        {
            lock(deserializationTasks)
            {
                DeserializationContext context = new DeserializationContext(r => onReceive.Invoke(remote, id, channel, r));

                context.task = serializer.Deserialize(data, context.SaveResult);

                deserializationTasks.Enqueue(context);
            }
        }

        public void Send(Guid remote, ushort channel, IBinarySerializable data)
        {
            lock(serializationTasks)
            {
                SerializationContext context = new SerializationContext(d => network.Send(remote, channel, d));

                context.task = serializer.Serialize(data, context.CopyData);

                serializationTasks.Enqueue(context);
            }
        }

        public void SendOthers(Guid remote, ushort channel, IBinarySerializable data)
        {
            lock(serializationTasks)
            {
                SerializationContext context = new SerializationContext(d => network.SendOthers(remote, channel, d));

                context.task = serializer.Serialize(data, context.CopyData);

                serializationTasks.Enqueue(context);
            }
        }

        public void SendAll(ushort channel, IBinarySerializable data)
        {
            lock(serializationTasks)
            {
                SerializationContext context = new SerializationContext(d => network.SendAll(channel, d));

                context.task = serializer.Serialize(data, context.CopyData);

                serializationTasks.Enqueue(context);
            }
        }
        #endregion

        #region Internal methods
        protected void Update<T>(Queue<T> queue, ref T context) where T : Context
        {
            int count;

            do
            {
                count = 0;

                if(context == null)
                {
                    lock(queue)
                    {
                        count = queue.Count;

                        if(count > 0)
                        {
                            context = queue.Dequeue();

                            count--;
                        }
                    }
                }

                if(context != null && !context.task.MoveNext())
                {
                    try
                    {
                        context.Execute();
                    }
                    catch(Exception e)
                    {
                        Debug.LogErrorFormat("{0}: {1}\n{2}", e.GetType(), e.Message, e.StackTrace);
                    }
                    finally
                    {
                        context = null;
                    }
                }
            }
            while(context == null && count > 0);
        }
        #endregion
    }
}

#endif // !NETFX_CORE
