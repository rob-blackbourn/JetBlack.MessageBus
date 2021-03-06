﻿using System.Collections.Generic;
using System.Linq;
using log4net;

namespace JetBlack.MessageBus.TopicBus.Adapters
{
    public class CachingPublisher<TData, TKey, TValue> where TData : IDictionary<TKey, TValue>, new()
    {
        private static readonly ILog Log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Client<TData> _client;
        private readonly Cache _cache;
        private readonly object _gate = new object();

        public CachingPublisher(Client<TData> client)
        {
            _client = client;
            _cache = new Cache(client);
            client.OnForwardedSubscription += (sender, args) =>
            {
                lock (_gate)
                {
                    if (args.IsAdd)
                        _cache.AddSubscription(args.ClientId, args.Topic);
                    else
                        _cache.RemoveSubscription(args.ClientId, args.Topic);
                }
            };
        }

        public void Publish(string topic, TData data)
        {
            lock (_gate)
            {
                _cache.Publish(topic, data);
            }
        }

        public void AddNotification(string topicPattern)
        {
            _client.AddNotification(topicPattern);
        }

        public void RemoveNotification(string topicPattern)
        {
            _client.RemoveNotification(topicPattern);
        }

        class Cache : Dictionary<string, CacheItem>
        {
            private readonly Client<TData> _client;

            public Cache(Client<TData> client)
            {
                _client = client;
            }

            public void AddSubscription(int clientId, string topic)
            {
                Log.DebugFormat("AddSubscription: clientId={0}, topic=\"{1}\"", clientId, topic);

                // Have we received a subscription or published data on this topic yet?
                CacheItem cacheItem;
                if (!TryGetValue(topic, out cacheItem))
                    Add(topic, cacheItem = new CacheItem());

                // Has this client already subscribed to this topic?
                if (!cacheItem.ClientStates.ContainsKey(clientId))
                {
                    // Add the client to the cache item, and indicate that we have not yet sent an image.
                    cacheItem.ClientStates.Add(clientId, false);
                }

                if (!(cacheItem.ClientStates[clientId] || Equals(cacheItem.Data, default(TData))))
                {
                    // Send the image and mark this client appropriately.
                    cacheItem.ClientStates[clientId] = true;

                    _client.Send(clientId, topic, true, cacheItem.Data);
                }
            }

            public void RemoveSubscription(int clientId, string topic)
            {
                Log.DebugFormat("RemoveSubscription: clientId={0}, topic=\"{1}\"", clientId, topic);

                // Have we received a subscription or published data on this topic yet?
                CacheItem cacheItem;
                if (!TryGetValue(topic, out cacheItem))
                    return;

                // Does this topic have this client?
                if (!cacheItem.ClientStates.ContainsKey(clientId))
                    return;

                cacheItem.ClientStates.Remove(clientId);

                // If there are no clients and no data remove the item.
                if (cacheItem.ClientStates.Count == 0 && Equals(cacheItem.Data, default(TData)))
                    Remove(topic);
            }

            public void Publish(string topic, TData data)
            {
                // If the topic is not in the cache add it.
                CacheItem cacheItem;
                if (!TryGetValue(topic, out cacheItem))
                    Add(topic, cacheItem = new CacheItem { Data = new TData() });

                // Bring the cache data up to date.
                foreach (var item in data)
                    cacheItem.Data[item.Key] = item.Value;

                foreach (var clientState in cacheItem.ClientStates.ToList())
                {
                    if (clientState.Value)
                        _client.Publish(topic, false, data);
                    else
                    {
                        // Deliver idividual messages to any clients yet to receive an image.
                        _client.Send(clientState.Key, topic, true, cacheItem.Data);
                        cacheItem.ClientStates[clientState.Key] = true;
                    }
                }
            }
        }

        class CacheItem
        {
            // Remember whether this client id has already received the image.
            public readonly Dictionary<int, bool> ClientStates = new Dictionary<int, bool>();
            // The cache of data constituting the image.
            public TData Data;
        }
    }
}
