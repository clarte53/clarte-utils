using System;
using UnityEngine;

namespace CLARTE.Threads.DataFlow.Unity {
    public class DataProvider<OuptputType>: MonoBehaviour, IMonoBehaviourDataProvider<OuptputType> {
        public IDataProvider<OuptputType> Provider { get { return DataCreator; } }

        protected DataFlow.DataProvider<OuptputType> DataCreator;

        protected virtual void Awake() {
            DataCreator = new DataFlow.DataProvider<OuptputType>();
            DataCreator.CreateData = CreateData;
        }

        protected virtual void Start() {
            DataCreator.Start();
        }

        protected virtual void Update() {
            if (DataCreator.HasException) {
                DataCreator.Stop();
            }
        }

        protected virtual void OnDestroy() {
            DataCreator.Stop();
        }

        protected virtual OuptputType CreateData() {
            throw new NotImplementedException();
        }
    }
}
