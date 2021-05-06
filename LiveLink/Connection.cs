using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExposedObject;
using LiveLink.Messages;
using NamedPipeWrapper;
using Newtonsoft.Json;

namespace LiveLink
{
    public class Connection : IDisposable
    {
        private NamedPipeServer<DataMessage> Server;
        private NamedPipeClient<DataMessage> Client;
        private readonly JsonSerializer Serializer = new();
        private readonly List<Func<Message, bool>> Handlers = new();

        public int ConnectionLostReported;
        public event Action OnConnectionLost;

        public Connection(bool isServer, int timeout = -1)
        {
            const string connectionName = "f5fb3673-ea79-4b29-87ea-a59d202a663c";

            if (isServer)
            {
                this.Server = new(connectionName);
                this.Server.ClientDisconnected += OnDisconnected;
                this.Server.ClientMessage += OnMessageReceived;
                this.Server.Start();
            }
            else
            {
                this.Client = new(connectionName);
                this.Client.ServerMessage += OnMessageReceived;
                this.Client.Disconnected += OnDisconnected;
                this.Client.AutoReconnect = false;

                if(timeout < 0)
                {
                    this.Client.Start();
                }
                else
                {
                    try
                    {
                        this.Client.StartSync(timeout);
                    }
                    catch (TimeoutException)
                    {
                        throw new TimeoutException("Server did not respond in time");
                    }
                }
            }
        }

        private void OnDisconnected(NamedPipeConnection<DataMessage, DataMessage> connection)
        {
            if(Interlocked.Exchange(ref ConnectionLostReported, 1) == 0)
            {
                this.OnConnectionLost?.Invoke();
            }
        }

        private void OnMessageReceived(NamedPipeConnection<DataMessage, DataMessage> connection, DataMessage data)
        {
            var message = this.Serializer.Deserialize(data);
            foreach (var handler in this.Handlers)
            {
                if (handler(message))
                    return;
            }
        }

        public void RegisterMessageHandler<TMessage>(Func<TMessage, bool> handler) where TMessage : Message
        {
            this.Handlers.Add(m =>
            {
                if (m is TMessage message)
                {
                    return handler(message);
                }

                return false;
            });
        }

        public void Send(Message message)
        {
            var data = this.Serializer.Serialize(message);
            this.Server?.PushMessage(data);
            this.Client?.PushMessage(data);
        }

        public static Task<Connection> ConnectToServerAsync(int millisecondsTimeout = 1000)
        {
            return Task.Run(() => new Connection(false, millisecondsTimeout));
        }

        //Note: This is hack af, but I'm too lazy to annotate all messages with [Serialize]
        [Serializable]
        private class DataMessage
        {
            public string Type;
            public byte[] Data;
        }

        private class JsonSerializer
        {
            public DataMessage Serialize(Message message)
            {
                var type = message.GetType().AssemblyQualifiedName;
                var json = JsonConvert.SerializeObject(message);
                return new DataMessage
                {
                    Type = type,
                    Data = Encoding.UTF8.GetBytes(json)
                };
            }

            public Message Deserialize(DataMessage dataMessage)
            {
                var type = Type.GetType(dataMessage.Type);
                var json = Encoding.UTF8.GetString(dataMessage.Data);
                return (Message)JsonConvert.DeserializeObject(json, type);
            }
        }

        static Connection()
        {
            //CLR is sometimes stupid and refuses to "find" assembly that is already loaded => Binary serializer fails
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var liveLink = Assembly.GetExecutingAssembly();
                if(liveLink.GetName().FullName == args.Name)
                {
                    return liveLink;
                }

                return null;
            };

        }

        ~Connection()
        {
            Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            
            this.Server?.Stop();
            this.Client?.Stop();
            
            this.Server = null;
            this.Client = null;
        }
    }
}
