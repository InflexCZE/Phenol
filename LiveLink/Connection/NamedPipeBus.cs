using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using LiveLink.Messages;

namespace LiveLink.Connection
{
    public interface IMessageFormatter
    {
        void Serialize<T>(Stream stream, T message) where T : Message;
        Message Deserialize(Stream stream);
    }

	public class NamedPipeBus
	{
		private readonly PipeName _pipeName;
		private readonly IMessageFormatter _formatter;
		private NamedPipeServerStream _server;
		private readonly HashSet<Guid> _ignoreMe; //What is this good for?

		public event Action<Message> OnMessageReceived;

		public NamedPipeBus(PipeName pipeName, IMessageFormatter formatter)
		{
			_pipeName = pipeName;
            _formatter = formatter;
			_ignoreMe = new HashSet<Guid>();
			_server = new NamedPipeServerStream
            (
				_pipeName.Read,
				PipeDirection.InOut,
				1,
				PipeTransmissionMode.Byte,
				PipeOptions.Asynchronous);

			//Debug.WriteLine($"Listening on pipe {_pipeName.Read}...");
			_server.BeginWaitForConnection(WaitForConnectionCallBack, null);
		}

		//TODO: Sending runs in byte-by-byte lockstep with other process.
		//App lags on one side affect both processes. Think about threaded sending
		//Note: Sending errors still need to be propagated
		public void Publish<T>(T msg) where T : Message
		{
			if (_ignoreMe.Contains(msg.MsgId))
			{
				_ignoreMe.Remove(msg.MsgId);
				return;
			}

            using var client = new NamedPipeClientStream
            (
				".",
				_pipeName.Write,
				PipeDirection.InOut,
				PipeOptions.None,
				System.Security.Principal.TokenImpersonationLevel.None
            );

            for(int i = 0; i < 10; i++)
            {
                if(_server is null)
                {
                    throw new ObjectDisposedException(nameof(NamedPipeBus));
                }

                try
                {
                    client.Connect(100);
			        _formatter.Serialize(client, msg);
                    return;
                }
                catch(TimeoutException)
                { }
            }

			throw new Exception("Could not deliver " + msg.GetType());
            
            //Debug.WriteLine($"[ => ] New message of type {msg.GetType().Name} sent to pipe {_pipeName.Write}");
		}

		private void WaitForConnectionCallBack(IAsyncResult result)
		{
			try
            {
                var server = Volatile.Read(ref _server);
				
                if(server is null)
					return;

				server.EndWaitForConnection(result);

				var message = _formatter.Deserialize(server);
				//Debug.WriteLine($"[ <= ] New message of type {message.GetType().Name} received on pipe {_pipeName.Read}");
				_ignoreMe.Add(message.MsgId);
                this.OnMessageReceived?.Invoke(message);
				server.Disconnect();

				server.BeginWaitForConnection(WaitForConnectionCallBack, null);
			}
			catch (ObjectDisposedException)
			{
				return;
			}
		}

		public void Dispose()
        {
            var server = Interlocked.Exchange(ref _server, null);
			server?.Dispose();
        }
	}
}