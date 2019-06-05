using System;

namespace CLARTE.Threads.DataFlow {
    public class NoDataProviderException: Exception {
        public NoDataProviderException() : base() { }
        public NoDataProviderException(string message) : base(message) { }
        public NoDataProviderException(string message, Exception innerException) : base(message, innerException) { }
    }
}
