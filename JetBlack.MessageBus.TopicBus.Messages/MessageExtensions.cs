﻿using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.ServiceModel.Channels;
using JetBlack.MessageBus.Common;
using JetBlack.MessageBus.Common.IO;
using JetBlack.MessageBus.Common.Network;

namespace JetBlack.MessageBus.TopicBus.Messages
{
    public static class MessageExtensions
    {
        public static IObservable<Message> ToMessageObservable(this Stream stream, BufferManager bufferManager)
        {
            return Observable.Create<Message>(
                observer => stream.ToFrameStreamAsyncObservable(bufferManager).Subscribe(
                    disposableBuffer =>
                    {
                        using (var messageStream = new MemoryStream(disposableBuffer.Value.Array, disposableBuffer.Value.Offset, disposableBuffer.Value.Count, false, false))
                        {
                            var message = Message.Read(messageStream);
                            observer.OnNext(message);
                        }
                        disposableBuffer.Dispose();
                    },
                    observer.OnError,
                    observer.OnCompleted));
        }

        public static IObserver<Message> ToMessageObserver(this Stream stream, BufferManager bufferManager)
        {
            var observer = stream.ToFrameStreamObserver();

            return Observer.Create<Message>(message =>
            {
                var messageStream = new BufferedMemoryStream(bufferManager, 256);
                message.Write(messageStream);
                var buffer = new ArraySegment<byte>(messageStream.GetBuffer(), 0, (int)messageStream.Length);
                observer.OnNext(DisposableValue.Create(buffer, messageStream));
            });
        }
    }
}