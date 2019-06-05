using CLARTE.Threads;
using System;

namespace CLARTE.Threads.DataFlow {
    public class DataProvider<OutputType>: IDataProvider<OutputType> {
        public event ProvideDataDelegate<OutputType> ProvideDataEvent;
        public CreateDataDelegate<OutputType> CreateData;

        public bool Running { get; private set; } = false;
        public bool HasException { get { return exception != null; } }

        private Thread thread;
        private Exception exception;

        public virtual void Start() {
            exception = null;
            thread = new Thread(new Action(threadedDataProvider));
            thread.Start();
        }

        public virtual void Stop() {
            Running = false;
            thread.Join();
            if (exception != null) {
                throw new Exception("Exception Occurs", exception);
            }
        }

        private void threadedDataProvider() {
            try {
                Running = true;
                while (Running) {
                    OutputType data = CreateData();
                    if (ProvideDataEvent != null) {
                        bool clone = ProvideDataEvent.GetInvocationList().Length > 1;
                        ProvideDataEvent.Invoke(data, clone);
                    }
                }
            } catch (Exception ex) {
                exception = ex;
            }
        }

    }
}
