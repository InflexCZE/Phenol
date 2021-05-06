using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace LiveLink.Messages
{
    public abstract partial class Request : Message
    { }
    
    public abstract class Response : Message
    {
        public Guid Request { get; set; }
    }

    public class RequestAttribute : Attribute
    {
        public Type RequestType { get; }

        public RequestAttribute(Type requestType)
        {
            this.RequestType = requestType;
        }
    }

    public static class Request<TResponse> where TResponse : Response
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly Type RequestType;

        static Request()
        {
            RequestType = typeof(TResponse).GetCustomAttribute<RequestAttribute>().RequestType;
        }

        public static Task<TResponse> From(Connection connection)
        {
            var request = Activator.CreateInstance(RequestType);
            return Request.From<TResponse>(connection, (Request) request);
        }
    }
    
    public partial class Request
    {
        private static readonly HashSet<Connection> KnownConnections = new();
        private static readonly ConcurrentDictionary<(Connection, Guid), Action<Response, Exception>> ResponseHandlers = new();

        public static Task<TResponse> From<TResponse>(Connection connection, Request request) where TResponse : Response
        {
            lock(KnownConnections)
            {
                if(KnownConnections.Add(connection))
                {
                    connection.RegisterMessageHandler<Response>(OnMessageReceived);
                    connection.OnConnectionLost += OnConnectionLost;

                    bool OnMessageReceived(Response response)
                    {
                        var key = (connection, response.Request);
                        if(ResponseHandlers.TryRemove(key, out var handler))
                        {
                            handler(response, null);
                            return true;
                        }
                        
                        Debug.Fail("Handler for response not found: " + response.GetType());
                        return false;
                    }

                    void OnConnectionLost()
                    {
                        bool wasKnown;
                        lock(KnownConnections)
                        {
                            wasKnown = KnownConnections.Remove(connection);
                        }

                        if(wasKnown)
                        {
                            foreach(var key in ResponseHandlers.Keys)
                            {
                                if(ReferenceEquals(connection, key.Item1))
                                {
                                    if(ResponseHandlers.TryRemove(key, out var handler))
                                    {
                                        handler(null, new Exception("ConnectionLost"));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var tcs = new TaskCompletionSource<TResponse>();
            ResponseHandlers.TryAdd((connection, request.MsgId), (response, exception) =>
            {
                if(exception is not null)
                {
                    tcs.TrySetException(exception);
                }
                else
                {
                    try
                    {
                        tcs.SetResult((TResponse)response);
                    }
                    catch(Exception e)
                    {
                        tcs.TrySetException(e);
                    }
                }
            });

            connection.Send(request);
            return tcs.Task;
        }
    }
}
