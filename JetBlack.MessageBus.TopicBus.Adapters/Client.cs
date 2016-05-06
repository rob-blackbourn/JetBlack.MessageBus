﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using BufferManager = System.ServiceModel.Channels.BufferManager;
using JetBlack.MessageBus.Common.IO;
using JetBlack.MessageBus.TopicBus.Messages;

namespace JetBlack.MessageBus.TopicBus.Adapters
{
    public class Client
    {
        public static async Task<Client<T>> Create<T>(IPEndPoint endpoint, IByteEncoder<T> byteEncoder, BufferManager bufferManager, IScheduler scheduler)
        {
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(endpoint.Address, endpoint.Port);

            return new Client<T>(tcpClient, byteEncoder, bufferManager, scheduler);
        }
    }

    public class Client<T> : Client, IDisposable
    {
        public event EventHandler<DataReceivedEventArgs<T>> OnDataReceived;
        public event EventHandler<ForwardedSubscriptionEventArgs> OnForwardedSubscription;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly TcpClient _tcpClient;
        private readonly IByteEncoder<T> _byteEncoder;
        private readonly IObserver<Message> _messageObserver;

        public Client(TcpClient tcpClient, IByteEncoder<T> byteEncoder, BufferManager bufferManager, IScheduler scheduler)
        {
            _tcpClient = tcpClient;
            _byteEncoder = byteEncoder;
            tcpClient.ToMessageObservable(bufferManager).SubscribeOn(scheduler).Subscribe(Dispatch, _cancellationTokenSource.Token);
            _messageObserver = tcpClient.ToMessageObserver(bufferManager);
        }

        private void Dispatch(Message message)
        {
            switch (message.MessageType)
            {
                case MessageType.MulticastData:
                    RaiseOnData(((MulticastData)message).Topic, ((MulticastData)message).Data, false);
                    break;
                case MessageType.UnicastData:
                    RaiseOnData(((UnicastData)message).Topic, ((UnicastData)message).Data, true);
                    break;
                case MessageType.ForwardedSubscriptionRequest:
                    RaiseOnForwardedSubscriptionRequest((ForwardedSubscriptionRequest)message);
                    break;
                default:
                    throw new ArgumentException("invalid message type");
            }
        }

        public void AddSubscription(string topic)
        {
            _messageObserver.OnNext(new SubscriptionRequest(topic, true));
        }

        public void RemoveSubscription(string topic)
        {
            _messageObserver.OnNext(new SubscriptionRequest(topic, false));
        }


        public void Send(int clientId, string topic, bool isImage, T data)
        {
            _messageObserver.OnNext(new UnicastData(clientId, topic, isImage, _byteEncoder.Encode(data)));
        }

        public void Publish(string topic, bool isImage, T data)
        {
            _messageObserver.OnNext(new MulticastData(topic, isImage, _byteEncoder.Encode(data)));
        }

        public void AddNotification(string topicPattern)
        {
            _messageObserver.OnNext(new NotificationRequest(topicPattern, true));
        }

        public void RemoveNotification(string topicPattern)
        {
            _messageObserver.OnNext(new NotificationRequest(topicPattern, false));
        }

        private void RaiseOnForwardedSubscriptionRequest(ForwardedSubscriptionRequest message)
        {
            var handler = OnForwardedSubscription;
            if (handler != null)
                handler(this, new ForwardedSubscriptionEventArgs(message.ClientId, message.Topic, message.IsAdd));
        }

        private void RaiseOnData(string topic, byte[] data, bool isImage)
        {
            var handler = OnDataReceived;
            if (handler != null)
                handler(this, new DataReceivedEventArgs<T>(topic, _byteEncoder.Decode(data), isImage));
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _tcpClient.Close();
        }
    }
}
