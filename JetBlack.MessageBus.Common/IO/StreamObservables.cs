﻿using System;
using System.IO;
using System.Reactive.Linq;

namespace JetBlack.MessageBus.Common.IO
{
    public static class StreamObservables
    {
        public static IObservable<ByteBuffer> ToStreamObservable(this Stream stream, int size)
        {
            return Observable.Create<ByteBuffer>(async (observer, token) =>
            {
                var buffer = new byte[size];

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var received = await stream.ReadAsync(buffer, 0, size, token);
                        if (received == 0)
                            break;

                        observer.OnNext(new ByteBuffer(buffer, received));
                    }

                    observer.OnCompleted();
                }
                catch (Exception error)
                {
                    observer.OnError(error);
                }
            });
        }
    }
}
