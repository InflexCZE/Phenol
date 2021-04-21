using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using LiveLink.Messages;

namespace LiveLink.Connection
{
    public class Connection : IDisposable
    {
        public event Action OnConnectionLost;
        public event Action OnClientConnected;

        private Endpoint Endpoint;
        private readonly List<Func<Message, bool>> Handlers = new();

        private Thread BeatThread;
        private long LastBeatTimestamp;
        private readonly TimeSpan BeatTime = TimeSpan.FromSeconds(1);

        //private bool EnabledBeats = true;
        private bool EnabledBeats = false;

        public Connection(Side side)
        {
            this.Endpoint = new Endpoint(side);
            this.Endpoint.OnMessageReceived += OnMessageReceived;

            if(side == Side.Out)
            {
                try
                {
                    StartBeatThread(true);
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            RegisterMessageHandler<HeartBeat>(_ =>
            {
                this.LastBeatTimestamp = DateTime.Now.Ticks;
                
                if(this.BeatThread == null)
                {
                    StartBeatThread(false);
                    OnClientConnected?.Invoke();
                }
                
                return true;
            });
        }

        private void StartBeatThread(bool isClient)
        {
            if(isClient)
            {
                this.Endpoint.Send(HeartBeat.Instance);
            }
            
            if(this.EnabledBeats == false)
                return;

            this.BeatThread = new Thread(() =>
            {
                while(true)
                {
                    Thread.Sleep(BeatTime);
                    
                    var endpoint = Interlocked.CompareExchange(ref this.Endpoint, null, null);
                    if(endpoint == null)
                        break;
                        
                    endpoint.Send(HeartBeat.Instance);

                    if(DateTime.Now - new DateTime(this.LastBeatTimestamp) > TimeSpan.FromTicks(BeatTime.Ticks * 5))
                    {
                        this.OnConnectionLost?.Invoke();
                        break;
                    }
                }

                this.BeatThread = null;
            })
            {
                IsBackground = true,
                Name = "BeatThread"
            };
            this.BeatThread.Start();
        }

        private class HeartBeat : Message
        {
            public static readonly HeartBeat Instance = new();
        }

        public void RegisterMessageHandler<TMessage>(Func<TMessage, bool> handler) where TMessage : Message
        {
            this.Handlers.Add(m =>
            {
                if(m is TMessage message)
                {
                    return handler(message);
                }

                return false;
            });
        }

        public void Send(Message message)
        {
            try
            {
                this.Endpoint.Send(message);
            }
            catch
            {
                this.OnConnectionLost?.Invoke();
            }
        }

        private void OnMessageReceived(Message message)
        {
            foreach(var handler in this.Handlers)
            {
                if(handler(message))
                    return;
            }
        }

        public static implicit operator Endpoint(Connection connection)
        {
            return connection.Endpoint;
        }

        public void Dispose()
        {
            var endpoint = Interlocked.Exchange(ref this.Endpoint, null);
            endpoint?.Dispose();
        }
    }
}
