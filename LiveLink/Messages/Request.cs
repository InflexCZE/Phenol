using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using LiveLink.Connection;

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

        public static Task<TResponse> From(Connection.Connection target)
        {
            var request = Activator.CreateInstance(RequestType);
            return Request.From<TResponse>(target, (Request) request);
        }
    }
    
    public partial class Request
    {
        private static readonly HashSet<Guid> KnownEndpoints = new();
        private static readonly ConcurrentDictionary<Guid, Action<Response>> ResponseHandlers = new();

        public static Task<TResponse> From<TResponse>(Connection.Connection target, Request request) where TResponse : Response
        {
            lock(KnownEndpoints)
            {
                var endpoint = (Endpoint) target;
                if(KnownEndpoints.Add(endpoint.GUID))
                {
                    endpoint.OnMessageReceived += OnMessageReceivedImpl;

                    static void OnMessageReceivedImpl(Message m)
                    {
                        if(m is Response response)
                        {
                            if(ResponseHandlers.TryRemove(response.Request, out var handler))
                            {
                                handler(response);
                            }
                            else
                            {
                                Debug.Fail("Handler for response not found: " + response.GetType());
                            }
                        }
                    }
                }
            }

            var tcs = new TaskCompletionSource<TResponse>();
            ResponseHandlers.TryAdd(request.MsgId, response =>
            {
                try
                {
                    tcs.SetResult((TResponse)response);
                }
                catch(Exception e)
                {
                    tcs.SetException(e);
                }
            });

            target.Send(request);
            return tcs.Task;
        }
    }
}
