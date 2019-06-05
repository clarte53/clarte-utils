namespace CLARTE.Threads.DataFlow {
    public delegate OutputType CreateDataDelegate<OutputType>();
    public delegate OutputType WorkOnDataDelegate<InputType, OutputType>(InputType data);
    public delegate void ConsumerDataDelegate<InputType>(InputType data);

    // Data transmission between chained workers
    public delegate void ProvideDataDelegate<OutputType>(OutputType data, bool clone);

    public interface IDataProvider<OutputType> {
        event ProvideDataDelegate<OutputType> ProvideDataEvent;
    }

    public interface IMonoBehaviourDataProvider<OuptputType> {
        IDataProvider<OuptputType> Provider { get; }
    }
}
