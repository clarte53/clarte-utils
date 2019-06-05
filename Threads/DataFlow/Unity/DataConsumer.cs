using System;
using UnityEngine;

namespace CLARTE.Threads.DataFlow.Unity {
    public abstract class DataConsumer<InputType>: MonoBehaviour where InputType : ICloneable {
        [SerializeField] protected GameObject DataProvider;
        [SerializeField] protected DataBarrier DataBarrier;
        [SerializeField] protected int millisecondsTimeout = 1000;

        protected DataFlow.DataConsumer<InputType> consumer;

        protected virtual void OnValidate() {
            if (DataProvider != null) {
                IMonoBehaviourDataProvider<InputType> monoprovider = DataProvider.GetComponent<IMonoBehaviourDataProvider<InputType>>();
                if (monoprovider == null) {
                    DataProvider = null;
                }
            }
        }

        protected void Awake() {
            // Create consumer
            consumer = new DataFlow.DataConsumer<InputType>();
            consumer.millisecondsTimeout = millisecondsTimeout;
            consumer.ConsumeData = ConsumeData;
            if (DataBarrier != null) {
                consumer.RegisterBarrier(DataBarrier.barrier);
            }
        }

        protected virtual void Start() {
            if (DataProvider != null) {
                var monoProvider = DataProvider.GetComponent<IMonoBehaviourDataProvider<InputType>>();
                monoProvider.Provider.ProvideDataEvent += consumer.EnqeueTask;
            } else {
                throw new NoDataProviderException("You have to set a DataProvider in Unity or in Awake.");
            }
        }

        protected virtual void Update() {
            if (consumer.HasException) {
                consumer.Throw();
            }
        }

        protected abstract void ConsumeData(InputType data);
    }
}
