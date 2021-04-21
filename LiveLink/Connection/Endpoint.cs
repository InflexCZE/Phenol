using System;
using System.IO;
using System.Reflection;
using System.Text;
using LiveLink.Messages;
using Newtonsoft.Json;

namespace LiveLink.Connection
{
    public class Endpoint : IDisposable
    {
        public Guid GUID { get; }
        public event Action<Message> OnMessageReceived;
        
        private NamedPipeBus Pipe;

        public Endpoint(Side side)
        {
            this.GUID = Guid.NewGuid();

            const string connectionName = "f5fb3673-ea79-4b29-87ea-a59d202a663c";
            var pipeName = new PipeName(connectionName, side);
            
            //Note: ProtoBuf is not serializing Message.MsgId => Use JSON instead
            this.Pipe = new NamedPipeBus(pipeName, new JsonFormatter());
            this.Pipe.OnMessageReceived += message => this.OnMessageReceived?.Invoke(message);
        }

        private class JsonFormatter : IMessageFormatter
        {
            private static readonly FieldInfo MsgId;

            static JsonFormatter()
            {
                MsgId = typeof(Message).GetField($"<{nameof(Message.MsgId)}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            public void Serialize<T>(Stream stream, T message) where T : Message
            {
                WriteString(message.GetType().AssemblyQualifiedName);
                WriteString(JsonConvert.SerializeObject(message));

                void WriteString(string value)
                {
                    var bytes = Encoding.UTF8.GetBytes(value);
                    WriteInt(bytes.Length);
                    stream.Write(bytes, 0, bytes.Length);
                }
                
                unsafe void WriteInt(int value)
                {
                    var ptr = (byte*)&value;
                    stream.WriteByte(*ptr++);
                    stream.WriteByte(*ptr++);
                    stream.WriteByte(*ptr++);
                    stream.WriteByte(*ptr);
                }
            }

            public Message Deserialize(Stream stream)
            {
                var typeName = ReadString();
                var jsonData = ReadString();

                var type = Type.GetType(typeName);
                return (Message) JsonConvert.DeserializeObject(jsonData, type);

                string ReadString()
                {
                    var n = ReadInt();
                    var bytes = new byte[n];
                    do
                    {
                        var read = stream.Read(bytes, 0, bytes.Length);
                        if(read == 0)
                            break;

                        n -= read;
                    } while(n > 0);

                    return Encoding.UTF8.GetString(bytes);
                }

                unsafe int ReadInt()
                {
                    int value = 0;
                    var ptr = (byte*)&value;
                    *ptr++ = (byte) stream.ReadByte();
                    *ptr++ = (byte) stream.ReadByte();
                    *ptr++ = (byte) stream.ReadByte();
                    *ptr = (byte) stream.ReadByte();
                    return value;
                }
            }
        }

        public void Send(Message message)
        {
            this.Pipe.Publish(message);
        }

        ~Endpoint()
        {
            Dispose();
        }

        public void Dispose()
        {
            this.Pipe?.Dispose();
            this.Pipe = null;

            GC.SuppressFinalize(this);
        }
    }
}
