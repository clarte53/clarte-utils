using System;
using UnityEngine;

namespace CLARTE.Threads.DataFlow.Unity {
    public abstract class DataWorker<InputType, OutputType>: MonoBehaviour, IMonoBehaviourDataProvider<OutputType> where InputType : ICloneable {
        [SerializeField] protected GameObject DataProvider;
        [SerializeField] protected DataBarrier DataBarrier;
        [SerializeField] protected int millisecondsTimeout = 1000;

        public IDataProvider<OutputType> Provider { get { return worker; } }
        protected DataFlow.DataWorker<InputType, OutputType> worker;

        protected virtual void OnValidate() {
            if (DataProvider != null) {
                IMonoBehaviourDataProvider<InputType> monoprovider = DataProvider.GetComponent<IMonoBehaviourDataProvider<InputType>>();
                if (monoprovider == null) {
                    DataProvider = null;
                }
            }
        }

        protected void Awake() {
            // Create worker
            worker = new DataFlow.DataWorker<InputType, OutputType>();
            worker.millisecondsTimeout = millisecondsTimeout;
            worker.WorkOnData = Work;
            if (DataBarrier != null) {
                worker.RegisterBarrier(DataBarrier.barrier);
            }
        }

        protected virtual void Start() {
            if (DataProvider != null) {
                var monoProvider = DataProvider.GetComponent<IMonoBehaviourDataProvider<InputType>>();
                monoProvider.Provider.ProvideDataEvent += worker.EnqeueTask;
            } else {
                throw new NoDataProviderException("You have to set a DataProvider in Unity or in Awake.");
            }
        }

        protected virtual void Update() {
            if (worker.HasException) {
                worker.Throw();
            }
        }

        protected abstract OutputType Work(InputType data);
    }
}
