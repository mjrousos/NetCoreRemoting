using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteExecution
{
    public class RemoteProxy : IDisposable
    {
        public Guid ID { get; }
        private string RemoteMachineName { get; }
        private string RemoteExecutionServerName { get; }
        private Type Type { get; }

        NamedPipeClientStream Pipe;

        internal RemoteProxy() { }

        public RemoteProxy(Type type, string remoteExecutionServerName, object[] parameters) : this(type, ".", remoteExecutionServerName, parameters) { }

        public RemoteProxy(Type type, string serverName, string remoteExecutionServerName, object[] parameters)
        {
            RemoteMachineName = serverName;
            RemoteExecutionServerName = remoteExecutionServerName;
            Type = type;

            InitializeAsync().Wait();

            // Request remote object be created
            var command = new RemoteCommand
            {
                 Command = Commands.NewObject,
                 Type = type,
                 Parameters = parameters,
                 ParameterTypes = parameters?.Select(p => p?.GetType()).ToArray()
            };

            ID = SendCommandAsync<Guid>(command).Result;
        }

        public RemoteProxy(string remoteExecutionServerName, Guid objectId) : this(".", remoteExecutionServerName, objectId) { }

        public RemoteProxy(string serverName, string remoteExecutionServerName, Guid objectId)
        {
            RemoteMachineName = serverName;
            RemoteExecutionServerName = remoteExecutionServerName;
            ID = objectId;

            InitializeAsync().Wait();

            // TODO
        }

        public async Task InvokeAsync(string methodName, params object[] parameters)
        {
            await InvokeAsync<object>(methodName, parameters);
        }

        public async Task<T> InvokeAsync<T>(string methodName, params object[] parameters)
        {
            var command = new RemoteCommand
            {
                ObjectId = ID,
                Command = Commands.Invoke,
                MethodName = methodName,
                Parameters = parameters,
                ParameterTypes = parameters?.Select(p => p?.GetType()).ToArray()
            };

            return await SendCommandAsync<T>(command);
        }

        public async Task InvokeStaticAsync(Type type, string methodName, params object[] parameters)
        {
            await InvokeStaticAsync<object>(type, methodName, parameters);
        }

        public async Task<T> InvokeStaticAsync<T>(Type type, string methodName, params object[] parameters)
        {
            var command = new RemoteCommand
            {
                Type = type,
                Command = Commands.Invoke,
                MethodName = methodName,
                Parameters = parameters,
                ParameterTypes = parameters?.Select(p => p?.GetType()).ToArray()
            };

            return await SendCommandAsync<T>(command);
        }

        private async Task InitializeAsync()
        {
            // Setup pipe
            Pipe = new NamedPipeClientStream(RemoteMachineName, RemoteExecutionServerName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await Pipe.ConnectAsync();
            Pipe.ReadMode = PipeTransmissionMode.Message;
        }
        
        private async Task<T> SendCommandAsync<T>(RemoteCommand command)
        {
            if (Pipe == null || !Pipe.IsConnected)
            {
                await InitializeAsync();
            }

            var commandString = SerializationHelper.Serialize(command);
            var commandBytes = Encoding.UTF8.GetBytes(commandString);
            await Pipe.WriteAsync(commandBytes, 0, commandBytes.Length);

            var returnBuffer = new byte[1024];
            var returnBytes = new List<byte>();
            do
            {
                var byteCount = await Pipe.ReadAsync(returnBuffer, 0, returnBuffer.Length);
                returnBytes.AddRange(returnBuffer.Take(byteCount));
                returnBuffer = new byte[1024];
            } while (!Pipe.IsMessageComplete);

            var returnString = Encoding.UTF8.GetString(returnBytes.ToArray());
            return SerializationHelper.Deserialize<T>(returnString);
        }

        public void Dispose()
        {
            if (Pipe != null && ID != Guid.Empty)
            {
                if (Pipe.IsConnected)
                {
                    SendCommandAsync<int>(new RemoteCommand
                    {
                        Command = Commands.CloseConnection,
                        ObjectId = ID,
                    }).Wait(1000);  // Wait up to a second before closing the pipe
                }
                Pipe.Dispose();
            }
        }
    }
}