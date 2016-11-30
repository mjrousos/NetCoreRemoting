using Newtonsoft.Json;
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

            Initialize();

            // Request remote object be created
            var command = new RemoteCommand
            {
                 Command = Commands.NewObject,
                 Type = type,
                 Parameters = parameters,
                 ParameterTypes = parameters.Select(p => p.GetType()).ToArray()
            };

            ID = SendCommand<Guid>(command).Result;
        }

        public RemoteProxy(string remoteExecutionServerName, Guid objectId) : this(".", remoteExecutionServerName, objectId) { }

        public RemoteProxy(string serverName, string remoteExecutionServerName, Guid objectId)
        {
            RemoteMachineName = serverName;
            RemoteExecutionServerName = remoteExecutionServerName;
            ID = objectId;

            Initialize();

            // TODO
        }

        private void Initialize()
        {
            JsonConvert.DefaultSettings = Common.GetJsonSettings;

            // Setup pipe
            Pipe = new NamedPipeClientStream(RemoteMachineName, RemoteExecutionServerName, PipeDirection.InOut, PipeOptions.Asynchronous);
            Pipe.Connect();
            Pipe.ReadMode = PipeTransmissionMode.Message;
        }
        
        private async Task<T> SendCommand<T>(RemoteCommand command)
        {
            var commandString = JsonConvert.SerializeObject(command);
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

            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(returnBytes.ToArray()));
        }

        public void Dispose()
        {
            // TODO - Send 'disconnect' message?

            if (Pipe != null)
            {
                Pipe.Dispose();
            }
        }
    }
}