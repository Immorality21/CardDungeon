using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmoralityGaming.Fundamentals
{
    /// <summary>
    /// A typed, synchronous event stream: subscribers name the event class they want, publishers raise
    /// instances of it, and every subscriber of that exact type is called in a fixed order.
    ///
    /// <para><b>Re-entrancy is a queue, not a stack.</b> An event raised while another is being
    /// dispatched - a handler whose reaction raises something new - is queued and delivered after the
    /// current one has reached every subscriber, in the order raised. A chain of reactions therefore
    /// resolves breadth-first and in a fixed order, and a runaway chain stops at <see cref="MaxChain"/>
    /// with an error rather than overflowing the stack.</para>
    ///
    /// <para>Subscribers are called in ascending <c>order</c>, then in the order they subscribed, so
    /// bookkeeping (order 0, the default) and reactions layered on top of it (a higher order) never
    /// race each other.</para>
    ///
    /// <para>Handlers run on the caller's thread and must not throw; an exception from one is logged
    /// and the rest of the subscribers still hear the event.</para>
    /// </summary>
    public class EventStream
    {
        /// <summary>Most events one outermost <see cref="Publish{T}"/> may deliver. A reaction that
        /// keeps feeding itself is a content bug; this turns it into a logged error instead of a hang.</summary>
        public const int MaxChain = 256;

        private sealed class Subscription
        {
            public Delegate Original;
            public Action<object> Invoke;
            public int Order;
            public int Sequence;
        }

        private readonly Dictionary<Type, List<Subscription>> _subscriptions = new Dictionary<Type, List<Subscription>>();

        // What a delivery iterates: a copy of each type's list, rebuilt only when that list changes, so
        // a handler may (un)subscribe mid-delivery without a copy per event - the simulator publishes
        // several events per hit across thousands of fights.
        private readonly Dictionary<Type, Subscription[]> _snapshots = new Dictionary<Type, Subscription[]>();
        private readonly Queue<object> _queue = new Queue<object>();
        private bool _dispatching;
        private int _sequence;

        /// <summary>Calls <paramref name="handler"/> for every <typeparamref name="T"/> raised on this
        /// stream. Subscribing the same handler twice calls it twice.</summary>
        public void Subscribe<T>(Action<T> handler, int order = 0) where T : class
        {
            if (handler == null)
            {
                return;
            }
            if (!_subscriptions.TryGetValue(typeof(T), out var list))
            {
                list = new List<Subscription>();
                _subscriptions[typeof(T)] = list;
            }
            list.Add(new Subscription
            {
                Original = handler,
                Invoke = evt => handler((T)evt),
                Order = order,
                Sequence = _sequence++
            });
            list.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Sequence.CompareTo(b.Sequence));
            _snapshots.Remove(typeof(T));
        }

        public void Unsubscribe<T>(Action<T> handler) where T : class
        {
            if (handler != null && _subscriptions.TryGetValue(typeof(T), out var list))
            {
                list.RemoveAll(s => Equals(s.Original, handler));
                _snapshots.Remove(typeof(T));
            }
        }

        /// <summary>Drops every subscription (and anything still queued).</summary>
        public void Clear()
        {
            _subscriptions.Clear();
            _snapshots.Clear();
            _queue.Clear();
        }

        /// <summary>
        /// Raises <paramref name="evt"/>. Delivered at once when nothing is being dispatched; queued
        /// behind the current event otherwise (see the class summary).
        /// </summary>
        public void Publish<T>(T evt) where T : class
        {
            if (evt == null)
            {
                return;
            }
            _queue.Enqueue(evt);
            Drain();
        }

        /// <summary>
        /// Raises two events back to back: <paramref name="second"/> is queued before anything
        /// <paramref name="first"/>'s handlers raise, so nothing can come between them. A killing blow and
        /// the death it causes go out this way - a reaction to the hit must not be heard before the death.
        /// </summary>
        public void Publish<T1, T2>(T1 first, T2 second) where T1 : class where T2 : class
        {
            if (first != null)
            {
                _queue.Enqueue(first);
            }
            if (second != null)
            {
                _queue.Enqueue(second);
            }
            Drain();
        }

        private void Drain()
        {
            if (_dispatching || _queue.Count == 0)
            {
                return;
            }

            _dispatching = true;
            int delivered = 0;
            try
            {
                while (_queue.Count > 0)
                {
                    if (++delivered > MaxChain)
                    {
                        Debug.LogError($"[{GetType().Name}] More than {MaxChain} events in one chain - a reaction is feeding itself. The rest are dropped.");
                        _queue.Clear();
                        break;
                    }
                    Deliver(_queue.Dequeue());
                }
            }
            finally
            {
                _dispatching = false;
            }
        }

        private void Deliver(object evt)
        {
            var type = evt.GetType();
            if (!_snapshots.TryGetValue(type, out var subscribers))
            {
                if (!_subscriptions.TryGetValue(type, out var list) || list.Count == 0)
                {
                    return;
                }
                subscribers = list.ToArray();
                _snapshots[type] = subscribers;
            }
            foreach (var subscription in subscribers)
            {
                try
                {
                    subscription.Invoke(evt);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
