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
        // ID and type of the remote object
        public Guid ID { get; }
        public Type Type { get; }

        // Connection information
        private string RemoteMachineName { get; }
        private string RemoteExecutionServerName { get; }

        // The pipe for communicating with the server
        NamedPipeClientStream Pipe;

        /// <summary>
        /// Create a new remote proxy for a given type
        /// </summary>
        /// <param name="type">The type to create remotely</param>
        /// <param name="remoteExecutionServerName">The name of the remote execution server (pipe name)</param>
        /// <param name="parameters">Parameters to pass to the objects constructor</param>
        public RemoteProxy(Type type, string remoteExecutionServerName, object[] parameters) : this(type, ".", remoteExecutionServerName, parameters) { }

        /// <summary>
        /// Create a new remote proxy for a given type
        /// </summary>
        /// <param name="type">The type to create remotely</param>
        /// <param name="serverName">The machine name where the remote server is running</param>
        /// <param name="remoteExecutionServerName">The name of the remote execution server (pipe name)</param>
        /// <param name="parameters">Parameters to pass to the objects constructor</param>
        public RemoteProxy(Type type, string serverName, string remoteExecutionServerName, object[] parameters)
        {
            RemoteMachineName = serverName;
            RemoteExecutionServerName = remoteExecutionServerName;
            Type = type;

            // Setup the pipe
            InitializeAsync().Wait();

            // Request remote object be created
            var command = new RemoteCommand
            {
                 Command = Commands.NewObject,
                 Type = type,
                 Parameters = parameters,
                 ParameterTypes = parameters?.Select(p => p?.GetType()).ToArray()
            };

            // Send the command and receive the remote object's ID back
            ID = SendCommandAsync<Guid>(command).Result;
        }

        /// <summary>
        /// Create a proxy for an existing remote object
        /// </summary>
        /// <param name="remoteExecutionServerName">The name of the remote execution server (pipe name)</param>
        /// <param name="objectId">The remote object's ID</param>
        public RemoteProxy(string remoteExecutionServerName, Guid objectId) : this(".", remoteExecutionServerName, objectId) { }

        /// <summary>
        /// Create a proxy for an existing remote object
        /// </summary>
        /// <param name="serverName">The machine name where the remote server is running</param>
        /// <param name="remoteExecutionServerName">The name of the remote execution server (pipe name)</param>
        /// <param name="objectId">The remote object's ID</param>
        public RemoteProxy(string serverName, string remoteExecutionServerName, Guid objectId)
        {
            RemoteMachineName = serverName;
            RemoteExecutionServerName = remoteExecutionServerName;
            ID = objectId;

            // Setup the pipe
            InitializeAsync().Wait();

            // Request remote object be created
            var command = new RemoteCommand
            {
                Command = Commands.RetrieveObject,
                ObjectId = ID
            };

            // Send the command and receive the remote object's ID back
            Type = SendCommandAsync<Type>(command).Result;
        }

        /// <summary>
        ///  Prepares named pipe communication with the server
        /// </summary>
        private async Task InitializeAsync()
        {
            // Setup pipe
            Pipe = new NamedPipeClientStream(RemoteMachineName, RemoteExecutionServerName, PipeDirection.InOut, PipeOptions.Asynchronous);

            // Connect and set ReadMode
            await Pipe.ConnectAsync();
            Pipe.ReadMode = PipeTransmissionMode.Message;
        }

        /// <summary>
        /// Invokes a method on the remote object
        /// </summary>
        /// <param name="methodName">Name of method to invoke</param>
        /// <param name="parameters">Method parameters</param>
        public async Task InvokeAsync(string methodName, params object[] parameters)
        {
            await InvokeAsync<object>(methodName, parameters);
        }

        /// <summary>
        /// Invokes a method on the remote object
        /// </summary>
        /// <typeparam name="T">The expected type of the returned object</typeparam>
        /// <param name="methodName">Name of method to invoke</param>
        /// <param name="parameters">Method parameters</param>
        public async Task<T> InvokeAsync<T>(string methodName, params object[] parameters)
        {
            var command = new RemoteCommand
            {
                ObjectId = ID,
                Command = Commands.Invoke,
                MemberName = methodName,
                Parameters = parameters,
                ParameterTypes = parameters?.Select(p => p?.GetType()).ToArray()
            };

            return await SendCommandAsync<T>(command);
        }

        /// <summary>
        /// Invoke a static method remotely
        /// </summary>
        /// <param name="type">The type containing the target method</param>
        /// <param name="methodName">The name of the method to invoke</param>
        /// <param name="parameters">Method parameters</param>
        public async Task InvokeStaticAsync(Type type, string methodName, params object[] parameters)
        {
            await InvokeStaticAsync<object>(type, methodName, parameters);
        }

        /// <summary>
        /// Invoke a static method remotely
        /// </summary>
        /// <typeparam name="T">The expected type of the returned object</typeparam>
        /// <param name="type">The type containing the target method</param>
        /// <param name="methodName">The name of the method to invoke</param>
        /// <param name="parameters">Method parameters</param>
        public async Task<T> InvokeStaticAsync<T>(Type type, string methodName, params object[] parameters)
        {
            var command = new RemoteCommand
            {
                Type = type,
                Command = Commands.Invoke,
                MemberName = methodName,
                Parameters = parameters,
                ParameterTypes = parameters?.Select(p => p?.GetType()).ToArray()
            };

            return await SendCommandAsync<T>(command);
        }

        /// <summary>
        /// Get a property value from the remote object
        /// </summary>
        /// <typeparam name="T">The expected type of the returned object</typeparam>
        /// <param name="propertyName">The property name to access</param>
        public async Task<T> GetPropertyAsync<T>(string propertyName)
        {
            var command = new RemoteCommand
            {
                ObjectId = ID,
                Command = Commands.GetProperty,
                MemberName = propertyName,
            };

            return await SendCommandAsync<T>(command);
        }

        /// <summary>
        /// Get a property value for a static property 
        /// </summary>
        /// <typeparam name="T">The expected type of the returned object</typeparam>
        /// <param name="type">The type to get the property from</param>
        /// <param name="propertyName">The property name to access</param>
        public async Task<T> GetStaticPropertyAsync<T>(Type type, string propertyName)
        {
            var command = new RemoteCommand
            {
                Type = type,
                Command = Commands.GetProperty,
                MemberName = propertyName,
            };

            return await SendCommandAsync<T>(command);
        }

        /// <summary>
        /// Sets a property on the remote object
        /// </summary>
        /// <param name="propertyName">The property name to set</param>
        /// <param name="value">The value to set the property to</param>
        public async Task SetPropertyAsync(string propertyName, object value)
        {
            var command = new RemoteCommand
            {
                ObjectId = ID,
                Command = Commands.SetProperty,
                MemberName = propertyName,
                Parameters = new[] { value }
            };

            await SendCommandAsync<object>(command);
        }

        /// <summary>
        /// Sets a static property remotely
        /// </summary>
        /// <param name="type">The type to set the property on</param>
        /// <param name="propertyName">The name of the property to set</param>
        /// <param name="value">The value to set the property to</param>
        public async Task SetStaticPropertyAsync(Type type, string propertyName, object value)
        {
            var command = new RemoteCommand
            {
                Type = type,
                Command = Commands.SetProperty,
                MemberName = propertyName,
                Parameters = new[] { value }
            };

            await SendCommandAsync<object>(command);
        }

        /// <summary>
        /// Sends a command to the remote server
        /// </summary>
        /// <typeparam name="T">The expected type of the returned object</typeparam>
        /// <param name="command">The command to execute remotely</param>
        /// <returns>The return value from executing the given command on the remote server</returns>
        private async Task<T> SendCommandAsync<T>(RemoteCommand command)
        {
            // If the pipe isn't setup yet, do so now
            if (Pipe == null || !Pipe.IsConnected)
            {
                await InitializeAsync();
            }

            // Serialize the command into bytes
            var commandString = SerializationHelper.Serialize(command);
            var commandBytes = Encoding.UTF8.GetBytes(commandString);

            // Write the serialized command bytes to the named pipe
            await Pipe.WriteAsync(commandBytes, 0, commandBytes.Length);

            // Read the return value from the named pipe
            // The remote server should alwasy send something back - even if it's just a null response
            var returnBuffer = new byte[1024];
            var returnBytes = new List<byte>();
            do
            {
                var byteCount = await Pipe.ReadAsync(returnBuffer, 0, returnBuffer.Length);
                returnBytes.AddRange(returnBuffer.Take(byteCount));
                returnBuffer = new byte[1024];
            } while (!Pipe.IsMessageComplete);

            // Deserialize the returned object
            var returnString = Encoding.UTF8.GetString(returnBytes.ToArray());
            return SerializationHelper.Deserialize<T>(returnString);
        }

        public void Dispose()
        {
            if (Pipe != null && ID != Guid.Empty)
            {
                if (Pipe.IsConnected)
                {
                    // If the pipe is setup and connected, request that it be disconnected
                    // and the ref count on the remote object be decreased.
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