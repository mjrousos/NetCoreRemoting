using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteExecution
{
    public class RemoteExecutionServer : IDisposable
    {
        // Dictionary for storing objects created remotely. Keyed by the guid that proxies will issue commands to the objects with
        ConcurrentDictionary<Guid, RemotelyCreatedObject> Objects = new ConcurrentDictionary<Guid, RemotelyCreatedObject>();

        // All current pipe connection tasks
        ConcurrentBag<Task> ConnectionTasks = new ConcurrentBag<Task>();

        CancellationTokenSource CTS = new CancellationTokenSource();

        // Whether new connection listening tasks may be started
        // Will be set to false during shutdown
        volatile bool Active = true;
        
        // This lock protects access to the Active bool
        ReaderWriterLockSlim ConnectionsActiveLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        // The name of the remote execution server (which proxies use to connect)
        public string RemoteName { get; set; }

        // Centralized logging so that we can easily log in a more sophisticated way in the future
        public Logger Logger { get; }

        /// <summary>
        /// Create a new RemoteExecutionServer with an auto-generated name
        /// </summary>
        public RemoteExecutionServer() : this(Guid.NewGuid().ToString()) { }

        /// <summary>
        /// Create a new RemoteExecutionServer
        /// </summary>
        /// <param name="name">The name that clients and proxies will use to connect</param>
        public RemoteExecutionServer(string name)
        {
            RemoteName = name;
            Logger = new Logger();
            QueueListener();
        }

        /// <summary>
        /// Starts listening for named pipe connections
        /// </summary>
        private void QueueListener()
        {
            // TODO - Clean up closed connections?

            // Make sure that we're not shutting down or anything like that
            ConnectionsActiveLock.EnterReadLock();
            try
            {
                if (Active)
                {
                    // If all is active, start listening
                    ConnectionTasks.Add(ListenForRemoteConnection());
                }
            }
            finally
            {
                ConnectionsActiveLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Listens for a remote connection via named pipes
        /// </summary>
        private async Task ListenForRemoteConnection()
        {
            using (var pipe = new NamedPipeServerStream(RemoteName, PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
            {
                // Wait for a remote client to connect
                await pipe.WaitForConnectionAsync(CTS.Token);

                Logger.Log("Client has connected");

                // Start a new listener since this one's now setting up an active connection
                QueueListener();

                // Loop processing messages from client
                while (!CTS.IsCancellationRequested && pipe.IsConnected)
                {
                    // Read message bytes
                    var messageBytes = new List<byte>();
                    do
                    {
                        var buffer = new byte[10];
                        var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, CTS.Token);
                        messageBytes.AddRange(buffer.Take(bytesRead));
                    }
                    while (!pipe.IsMessageComplete && !CTS.IsCancellationRequested);

                    if (pipe.IsMessageComplete && messageBytes.Count > 0)
                    {
                        Logger.Log($"Received {messageBytes} bytes from client", MessagePriority.Verbose);

                        // Process the message
                        object returnVal = null;
                        bool shouldCloseConnection = false;
                        ProcessMessage(messageBytes.ToArray(), ref returnVal, ref shouldCloseConnection);

                        // Serialize any return value and write it to the pipe
                        var returnString = SerializationHelper.Serialize(returnVal);
                        var returnBytes = Encoding.UTF8.GetBytes(returnString);
                        Logger.Log($"Writing {returnBytes.Length} bytes to client", MessagePriority.Verbose);
                        await pipe.WriteAsync(returnBytes, 0, returnBytes.Length, CTS.Token);

                        // If processing resulted in a request to disconnect, do so
                        if (shouldCloseConnection)
                        {
                            Logger.Log("Disconnecting from client");
                            pipe.Disconnect();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Deserializes a command from a message and acts on it
        /// </summary>
        private void ProcessMessage(byte[] messageBytes, ref object returnVal, ref bool shouldCloseConnection)
        {
            RemoteCommand command = null;
            var commandString = new string(Encoding.UTF8.GetChars(messageBytes));

            try
            {
                // Deserialize command
                command = SerializationHelper.Deserialize<RemoteCommand>(commandString);
                Logger.Log($"Received command {command.Command}", MessagePriority.Informational);
                Logger.Log($"  Object ID:   {command.ObjectId}", MessagePriority.Verbose);
                Logger.Log($"  Type:        {command.Type?.AssemblyQualifiedName}", MessagePriority.Verbose);
                Logger.Log($"  Member Name: {command.MemberName}", MessagePriority.Verbose);
                Logger.Log($"  Param count: {command.Parameters?.Length}", MessagePriority.Verbose);
            }
            catch (ArgumentException)
            {
                Logger.Log($"Invalid message not processed: {commandString}", MessagePriority.Warning);
                return;
            }

            switch (command.Command)
            {
                case Commands.CloseConnection:
                    // Decrement the ref-count for the object referenced by the closing connection
                    RemotelyCreatedObject remotelyCreatedObject;
                    if (Objects.TryGetValue(command.ObjectId, out remotelyCreatedObject))
                    {
                        if (0 >= Interlocked.Decrement(ref remotelyCreatedObject.RefCount))
                        {
                            // If no references remain, end-of-life the object and remove it from the dictionary
                            //
                            // If we need to adjust remote object lifetime (should a future process be able to get an old object?)
                            // we can change that behavior here.
                            Logger.Log($"Removing object {command.ObjectId} which is no longer referenced remotely");
                            remotelyCreatedObject.Value = null;
                            Objects.TryRemove(command.ObjectId, out remotelyCreatedObject);
                        }
                    }

                    // Record that a disconnection has been requested
                    shouldCloseConnection = true;
                    break;
                case Commands.NewObject:
                    if (command.Type != null)
                    {
                        object newObj = null;
                        try
                        {
                            if (command.Parameters?.Length > 0)
                            {
                                // Map parameters to their proper types
                                object[] ctorParameters = new object[command.Parameters.Length];
                                for (int i = 0; i < command.Parameters.Length; i++)
                                { 
                                    ctorParameters[i] = SerializationHelper.GetObjectAsType(command.Parameters[i], command.ParameterTypes[i]);
                                }

                                // Create object
                                newObj = Activator.CreateInstance(command.Type, ctorParameters);
                            }
                            else
                            {
                                // If the type has a default ctor or is a value type, allow creation without parameters
                                if (command.Type.GetTypeInfo().IsValueType || command.Type.GetTypeInfo().DeclaredConstructors.Any(c => c.GetParameters().Length == 0))
                                {
                                    newObj = Activator.CreateInstance(command.Type);
                                }
                            }
                        }
                        catch (Exception exc)
                        {
                            Logger.Log($"WARNING: Failed to create object of type {command.Type.AssemblyQualifiedName}: {exc.ToString()}", MessagePriority.Warning);
                        }

                        if (newObj != null)
                        {
                            // Register the object in our dictionary
                            var newId = Guid.NewGuid();
                            Objects.TryAdd(newId, new RemotelyCreatedObject { RefCount = 1, Value = newObj });

                            // Set the return value to the object's ID (to be returned to the client)
                            returnVal = newId;

                            Logger.Log($"Created a new objet with ID {newId.ToString()} and type {command.Type.FullName}", MessagePriority.Informational);
                        }
                    }
                    break;
                case Commands.RetrieveObject:
                    // TODO - Look up an object and return its type. Increment its ref count in the dictionary
                    //        This functionality isn't strictly necessary, but mirrors traditional remoting's ability
                    //        to retrieve a remote object previously created by another remote client.
                    break;
                case Commands.Invoke:
                    MethodInfo method = null;
                    object objectToInvokeOn = null;

                    // Lookup method via Reflection
                    if (command.Type != null)
                    {
                        // Static method
                        method = command.Type.GetRuntimeMethod(command.MemberName, command.ParameterTypes);
                    }
                    else if (command.ObjectId != Guid.NewGuid())
                    {
                        // Instance method
                        RemotelyCreatedObject obj = null;
                        if (Objects.TryGetValue(command.ObjectId, out obj))
                        {
                            objectToInvokeOn = obj?.Value;
                            method = objectToInvokeOn?.GetType().GetRuntimeMethod(command.MemberName, command.ParameterTypes);
                        }
                    }

                    // Get parameters
                    object[] parameters = null;
                    if (command.Parameters?.Length > 0)
                    {
                        parameters = new object[command.Parameters.Length];
                        for (int i = 0; i < command.Parameters.Length; i++)
                        {
                            parameters[i] = SerializationHelper.GetObjectAsType(command.Parameters[i], command.ParameterTypes[i]);
                        }
                    }

                    // Invoke method
                    if (method != null)
                    {
                        Logger.Log($"Invoking {method.DeclaringType.Name}.{method.Name}");
                        returnVal = method.Invoke(objectToInvokeOn, parameters);
                        Logger.Log($"Return value: {returnVal?.ToString()}", MessagePriority.Verbose);
                    }

                    break;
                case Commands.GetProperty:
                case Commands.SetProperty:
                    PropertyInfo property = null;
                    object objectToAccess = null;
                    // Lookup property via Reflection
                    if (command.Type != null)
                    {
                        // Static property
                        property = command.Type.GetRuntimeProperty(command.MemberName);
                    }
                    else if (command.ObjectId != Guid.NewGuid())
                    {
                        // Instance property
                        RemotelyCreatedObject obj = null;
                        if (Objects.TryGetValue(command.ObjectId, out obj))
                        {
                            objectToAccess = obj?.Value;
                            property = objectToAccess?.GetType().GetRuntimeProperty(command.MemberName);
                        }
                    }

                    // Get parameters
                    object parameter = null;
                    if (command.Parameters?.Length > 0)
                    {
                        parameter = SerializationHelper.GetObjectAsType(command.Parameters[0], command.ParameterTypes[0]);
                    }

                    // Get/Set the property
                    if (property != null)
                    {
                        if (command.Command == Commands.GetProperty)
                        {
                            Logger.Log($"Getting property {property.DeclaringType.Name}.{property.Name}");
                            returnVal = property.GetValue(objectToAccess);
                            Logger.Log($"Return value: {returnVal?.ToString()}", MessagePriority.Verbose);
                        }
                        else if (command.Command == Commands.SetProperty)
                        {
                            Logger.Log($"Setting property {property.DeclaringType.Name}.{property.Name}");
                            property.SetValue(objectToAccess, parameter);
                        }
                    }

                    break;
                default:
                    break;
            }
        }
        
        public void Dispose()
        {
            // Prevent new tasks from beginning
            ConnectionsActiveLock.EnterWriteLock();
            Active = false;
            ConnectionsActiveLock.ExitWriteLock();

            // Cancel existing tasks and wait for them to finish
            CTS.Cancel();
            Task.WaitAll(ConnectionTasks.ToArray(), 5000);
        }
    }
}
