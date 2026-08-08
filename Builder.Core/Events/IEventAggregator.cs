namespace Builder.Core.Events;

public interface IEventAggregator
{
    void Send<TArgs>(TArgs args) where TArgs : EventBase;

    void Subscribe(object subscriber);
}
