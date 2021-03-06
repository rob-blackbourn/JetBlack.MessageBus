﻿using System;
using System.Net;
using JetBlack.MessageBus.FeedBus.Messages;
using JetBlack.MessageBus.FeedBus.Distributor.Config;

namespace JetBlack.MessageBus.FeedBus.Distributor
{
    internal interface IInteractor : IDisposable, IEquatable<IInteractor>, IComparable<IInteractor>
    {
        int Id { get; }
        string Name { get; }
        IPAddress IPAddress { get; }

        IObservable<Message> ToObservable();
        void SendMessage(Message message);
        bool HasRole(string feed, ClientRole role);
    }
}
