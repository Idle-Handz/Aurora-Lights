namespace Builder.Core.Events;

public interface ISubscriber<TArgs> where TArgs : EventBase
{
    void OnHandleEvent(TArgs args);
}
