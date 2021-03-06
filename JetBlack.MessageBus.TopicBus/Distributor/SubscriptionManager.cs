﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using JetBlack.MessageBus.TopicBus.Messages;
using log4net;

namespace JetBlack.MessageBus.TopicBus.Distributor
{
    internal class Subscription
    {
        public Subscription(Interactor subscriber, string topic)
        {
            Subscriber = subscriber;
            Topic = topic;
        }

        public Interactor Subscriber { get; private set; }
        public string Topic { get; private set; }
    }

    internal class SubscriptionRepository
    {
        // Topic->Interactor->SubscriptionCount.
        private readonly IDictionary<string, IDictionary<Interactor, int>> _cache = new Dictionary<string, IDictionary<Interactor, int>>();
    }

    internal class SubscriptionManager
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly NotificationMarshaller _notificationMarshaller;
        private readonly PublisherMarshaller _publisherMarshaller;

        public SubscriptionManager(NotificationMarshaller notificationMarshaller, PublisherMarshaller publisherMarshaller)
        {
            _notificationMarshaller = notificationMarshaller;
            _publisherMarshaller = publisherMarshaller;
        }

        // Topic->Interactor->SubscriptionCount.
        private readonly IDictionary<string, IDictionary<Interactor, int>> _cache = new Dictionary<string, IDictionary<Interactor, int>>();

        public void RequestSubscription(Interactor subscriber, SubscriptionRequest subscriptionRequest)
        {
            Log.DebugFormat("Received subscription from {0} on \"{1}\"", subscriber, subscriptionRequest);

            if (subscriptionRequest.IsAdd)
                AddSubscription(subscriber, subscriptionRequest.Topic);
            else
                RemoveSubscription(subscriber, subscriptionRequest.Topic);

            _notificationMarshaller.ForwardSubscription(subscriber, subscriptionRequest);
        }

        private void AddSubscription(Interactor subscriber, string topic)
        {
            // Find the list of interactors that have subscribed to this topic.
            IDictionary<Interactor, int> subscribersForTopic;
            if (!_cache.TryGetValue(topic, out subscribersForTopic))
                _cache.Add(topic, new Dictionary<Interactor, int> { { subscriber, 1 } });
            else if (!subscribersForTopic.ContainsKey(subscriber))
                subscribersForTopic.Add(subscriber, 1);
            else
                ++subscribersForTopic[subscriber];
        }

        private void RemoveSubscription(Interactor subscriber, string topic)
        {
            // Can we find this topic in the cache?
            IDictionary<Interactor, int> subscribersForTopic;
            if (!_cache.TryGetValue(topic, out subscribersForTopic))
                return;

            // Has this subscriber registered an interest in the topic?
            if (!subscribersForTopic.ContainsKey(subscriber))
                return;

            // Decrement the subscription count, and if there are none left, remove it.
            if (--subscribersForTopic[subscriber] == 0)
                subscribersForTopic.Remove(subscriber);

            // If there are no subscribers left on this topic, remove it from the cache.
            if (subscribersForTopic.Count == 0)
                _cache.Remove(topic);
        }

        public void OnFaultedInteractor(Interactor interactor, Exception error)
        {
            Log.Warn("Interactor faulted: " + interactor, error);

            OnClosedInteractor(interactor);
        }

        public void OnClosedInteractor(Interactor interactor)
        {
            Log.DebugFormat("Removing subscriptions for {0}", interactor);

            // Remove the subscriptions
            var topicsSubscribedTo = new List<string>();
            var topicsWithoutSubscribers = new List<string>();
            foreach (var subscription in _cache.Where(x => x.Value.ContainsKey(interactor)))
            {
                topicsSubscribedTo.Add(subscription.Key);

                subscription.Value.Remove(interactor);
                if (subscription.Value.Count == 0)
                    topicsWithoutSubscribers.Add(subscription.Key);
            }

            foreach (var topic in topicsWithoutSubscribers)
                _cache.Remove(topic);

            // Inform those interested that this interactor is no longer subscribed to these topics.
            foreach (var subscriptionRequest in topicsSubscribedTo.Select(topic => new SubscriptionRequest(topic, false)))
                _notificationMarshaller.ForwardSubscription(interactor, subscriptionRequest);
        }

        public void SendUnicastData(Interactor publisher, UnicastData unicastData)
        {
            // Are there subscribers for this topic?
            IDictionary<Interactor, int> subscribers;
            if (!_cache.TryGetValue(unicastData.Topic, out subscribers))
                return;

            // Can we find this client in the subscribers to this topic?
            var subscriber = subscribers.FirstOrDefault(x => x.Key.Id == unicastData.ClientId).Key;
            if (subscriber == null)
                return;

            _publisherMarshaller.SendUnicastData(publisher, subscriber, unicastData);
        }

        public void SendMulticastData(Interactor publisher, MulticastData multicastData)
        {
            // Are there subscribers for this topic?
            IDictionary<Interactor, int> subscribers;
            if (!_cache.TryGetValue(multicastData.Topic, out subscribers))
                return;

            _publisherMarshaller.SendMulticastData(publisher, subscribers.Keys, multicastData);
        }

        public void OnNewNotificationRequest(Interactor requester, Regex topicRegex)
        {
            // Find the subscribers whoes subscriptions match the pattern.
            foreach (var matchingSubscriptions in _cache.Where(x => topicRegex.IsMatch(x.Key)))
            {
                // Tell the requestor about subscribers that are interested in this topic.
                foreach (var subscriber in matchingSubscriptions.Value.Keys)
                    requester.SendMessage(new ForwardedSubscriptionRequest(subscriber.Id, matchingSubscriptions.Key, true));
            }
        }

        public void OnStaleTopics(IEnumerable<string> staleTopics)
        {
            foreach (var staleTopic in staleTopics)
                OnStaleTopic(staleTopic);
        }

        private void OnStaleTopic(string staleTopic)
        {
            IDictionary<Interactor, int> subscribersForTopic;
            if (!_cache.TryGetValue(staleTopic, out subscribersForTopic))
                return;

            // Inform subscribers by sending an image with no data.
            var staleMessage = new MulticastData(staleTopic, true, null);
            foreach (var subscriber in subscribersForTopic.Keys)
                subscriber.SendMessage(staleMessage);
        }
    }
}
