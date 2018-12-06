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

        protected class SerializationContext : Context
        {
            #region Members
            public Action<byte[]> callback;
            public byte[] data;
            #endregion

            #region Constructors
            public SerializationContext(Action<byte[]> callback)
            {
                this.callback = callback;

                task = null;
                data = null;
            }
            #endregion

            #region Public methods
            public override void Execute()
            {
                callback(data);
            }

            public void CopyData(byte[] buffer, uint size)
            {
                Array.Copy(buffer, data, size);
            }
            #endregion
        }

        protected class DeserializationContext : Context
        {
            #region Members
            public Action<IBinarySerializable> callback;
            public IBinarySerializable received;
            #endregion

            #region Constructors
            public DeserializationContext(Action<IBinarySerializable> callback)
            {
                this.callback = callback;

                task = null;
                received = null;
            }
            #endregion

            #region Public methods
            public override void Execute()
            {
                callback(received);
            }

            public void SaveResult(IBinarySerializable received)
            {
                this.received = received;
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
            DeserializationContext context = new DeserializationContext(r => onReceive.Invoke(remote, id, channel, r));

            context.task = serializer.Deserialize<IBinarySerializable>(data, context.SaveResult);

            deserializationTasks.Enqueue(context);
        }

        public void Send(Guid remote, ushort channel, IBinarySerializable data)
        {
            Send(new SerializationContext(d => network.Send(remote, channel, d)), data);
        }

        public void SendOthers(Guid remote, ushort channel, IBinarySerializable data)
        {
            Send(new SerializationContext(d => network.SendOthers(remote, channel, d)), data);
        }

        public void SendAll(ushort channel, IBinarySerializable data)
        {
            Send(new SerializationContext(d => network.SendAll(channel, d)), data);
        }
        #endregion

        #region Internal methods
        protected void Send(SerializationContext context, IBinarySerializable data)
        {
            context.task = serializer.Serialize(data, context.CopyData);

            serializationTasks.Enqueue(context);
        }

        protected void Update<T>(Queue<T> queue, ref T context) where T : Context
        {
            do
            {
                if(context == null && queue.Count > 0)
                {
                    context = queue.Dequeue();
                }

                if(context != null && !context.task.MoveNext())
                {
                    context.Execute();

                    context = null;
                }
            }
            while(context == null && queue.Count > 0);
        }
        #endregion
    }
}
