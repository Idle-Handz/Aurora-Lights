using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Builder.Core.Logging;

namespace Builder.Core.Events;

public class EventAggregator : IEventAggregator
{
    private readonly Dictionary<Type, List<WeakReference>> _subscribers;
    private readonly object _lock;

    public EventAggregator()
    {
        Logger.Initializing(this);
        _subscribers = new Dictionary<Type, List<WeakReference>>();
        _lock = new object();
    }

    public void Send<TArgs>(TArgs args) where TArgs : EventBase
    {
        Type subsriberType = typeof(ISubscriber<>).MakeGenericType(typeof(TArgs));
        List<WeakReference> subscriberList = GetSubscriberList(subsriberType);
        List<WeakReference> expiredSubscribers = new List<WeakReference>();

        foreach (WeakReference item in subscriberList)
        {
            if (item.IsAlive)
            {
                ISubscriber<TArgs> subscriber = (ISubscriber<TArgs>)item.Target;
                InvokeSubscriberEvent(args, subscriber);
            }
            else
            {
                expiredSubscribers.Add(item);
            }
        }

        if (!expiredSubscribers.Any())
        {
            return;
        }

        lock (_lock)
        {
            foreach (WeakReference item in expiredSubscribers)
            {
                subscriberList.Remove(item);
            }
        }
    }

    public void Subscribe(object subscriber)
    {
        lock (_lock)
        {
            IEnumerable<Type> subscriberTypes =
                from interfaceType in subscriber.GetType().GetInterfaces()
                where interfaceType.IsGenericType
                    && interfaceType.GetGenericTypeDefinition() == typeof(ISubscriber<>)
                select interfaceType;

            WeakReference reference = new WeakReference(subscriber);
            foreach (Type subscriberType in subscriberTypes)
            {
                GetSubscriberList(subscriberType).Add(reference);
            }
        }
    }

    private void InvokeSubscriberEvent<TArgs>(TArgs args, ISubscriber<TArgs> subscriber)
        where TArgs : EventBase
    {
        (SynchronizationContext.Current ?? new SynchronizationContext()).Post(
            _ => subscriber.OnHandleEvent(args),
            null);
    }

    private List<WeakReference> GetSubscriberList(Type subsriberType)
    {
        List<WeakReference> value = null;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(subsriberType, out value))
            {
                value = new List<WeakReference>();
                _subscribers.Add(subsriberType, value);
            }
        }

        return value;
    }
}
