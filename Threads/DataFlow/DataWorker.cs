using System;
using System.Threading;

namespace CLARTE.Threads.DataFlow {
    public class DataWorker<InputType, OutputType>: IDataProvider<OutputType> where InputType: ICloneable {
        public event ProvideDataDelegate<OutputType> ProvideDataEvent;
        public WorkOnDataDelegate<InputType, OutputType> WorkOnData;
        public int millisecondsTimeout = 1000;

        public bool HasException { get { return exception != null; } }

        private InputType inputData;
        private Exception exception;
        private Barrier barrier;

        private AutoResetEvent enqueue = new AutoResetEvent(true);

        public void RegisterBarrier(Barrier barrier) {
            if (this.barrier != null) {
                this.barrier.RemoveParticipant();
            }
            this.barrier = barrier;
            this.barrier.AddParticipant();
        }

        public void EnqeueTask(InputType data, bool clone) {
            inputData = clone ? (InputType)data.Clone() : data;
            if (!enqueue.WaitOne(millisecondsTimeout)) {
                throw new TimeoutException(string.Format("ConsumeData is too long, new data is waiting for {0} milliseconds.", millisecondsTimeout));
            }
            if (barrier != null && !barrier.SignalAndWait(millisecondsTimeout)) {
                throw new TimeoutException(string.Format("A barrier participant is too long, new data is waiting for {0} milliseconds.", millisecondsTimeout));
            }
            Tasks.Add(new Action(asyncWork));
        }

        public void Throw() {
            if (exception != null) {
                throw new Exception("Exception Occurs", exception);
            }
        }

        private void asyncWork() {
            try {
                OutputType data = WorkOnData(inputData);
                if (ProvideDataEvent != null) {
                    bool clone = ProvideDataEvent.GetInvocationList().Length > 1;
                    ProvideDataEvent.Invoke(data, clone);
                }
            } catch (Exception ex) {
                exception = ex;
            }
            enqueue.Set();
        }
    }
}
