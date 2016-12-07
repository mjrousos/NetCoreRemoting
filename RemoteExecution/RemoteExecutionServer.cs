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
        ConcurrentDictionary<Guid, RemotelyCreatedObject> Objects = new ConcurrentDictionary<Guid, RemotelyCreatedObject>();
        ConcurrentBag<Task> ConnectionTasks = new ConcurrentBag<Task>();
        CancellationTokenSource CTS = new CancellationTokenSource();

        // Whether new connection listening tasks may be started
        // Will be set to false during shutdown
        volatile bool Active = true;
        // This lock protects access to the Active bool
        ReaderWriterLockSlim ConnectionsActiveLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public string RemoteName { get; set; }

        public RemoteExecutionServer() : this(Guid.NewGuid().ToString()) { }

        public RemoteExecutionServer(string name)
        {
            RemoteName = name;
            QueueListener();
        }

        private void QueueListener()
        {
            // TODO - Clean up closed connections?
            ConnectionsActiveLock.EnterReadLock();
            try
            {
                if (Active)
                {
                    ConnectionTasks.Add(ListenForRemoteConnection());
                }
            }
            finally
            {
                ConnectionsActiveLock.ExitReadLock();
            }
        }

        private async Task ListenForRemoteConnection()
        {
            using (var pipe = new NamedPipeServerStream(RemoteName, PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
            {
                // Wait for a remote client to connect
                await pipe.WaitForConnectionAsync(CTS.Token);

                QueueListener();

                // Loop processing messages from client
                while (!CTS.IsCancellationRequested && pipe.IsConnected)
                {
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
                        object returnVal = null;
                        bool shouldCloseConnection = false;
                        ProcessMessage(messageBytes.ToArray(), ref returnVal, ref shouldCloseConnection);

                        var returnString = SerializationHelper.Serialize(returnVal);
                        var returnBytes = Encoding.UTF8.GetBytes(returnString);
                        await pipe.WriteAsync(returnBytes, 0, returnBytes.Length, CTS.Token);

                        if (shouldCloseConnection)
                        {
                            pipe.Disconnect();
                        }
                    }
                }
            }
        }

        private void ProcessMessage(byte[] messageBytes, ref object returnVal, ref bool shouldCloseConnection)
        {
            RemoteCommand command = null;
            var commandString = new string(Encoding.UTF8.GetChars(messageBytes));

            try
            {
                command = SerializationHelper.Deserialize<RemoteCommand>(commandString);
                Console.WriteLine("Received command: ");
                Console.WriteLine($"\tCommand: {command.Command}");
                Console.WriteLine($"\tObject id: {command.ObjectId}");
                Console.WriteLine($"\tMethod name: {command.MemberName}");
                Console.WriteLine($"\tParameter count: {command.Parameters?.Length ?? 0}");
                Console.WriteLine($"\tType: {command.Type?.AssemblyQualifiedName}");
            }
            catch (ArgumentException)
            {
                Console.WriteLine("WARNING: Invalid message not processed:");
                Console.WriteLine($"    {commandString}");
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
                            remotelyCreatedObject.Value = null;
                            Objects.TryRemove(command.ObjectId, out remotelyCreatedObject);
                        }
                    }
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
                                object[] ctorParameters = new object[command.Parameters.Length];
                                for (int i = 0; i < command.Parameters.Length; i++)
                                { 
                                    ctorParameters[i] = SerializationHelper.GetObjectAsType(command.Parameters[i], command.ParameterTypes[i]);
                                }
                                newObj = Activator.CreateInstance(command.Type, ctorParameters);
                            }
                            else
                            {
                                if (command.Type.GetTypeInfo().IsValueType || command.Type.GetTypeInfo().DeclaredConstructors.Any(c => c.GetParameters().Length == 0))
                                {
                                    newObj = Activator.CreateInstance(command.Type);
                                }
                            }
                        }
                        catch (Exception exc)
                        {
                            Console.WriteLine($"WARNING: Failed to create object of type {command.Type.AssemblyQualifiedName}");
                            Console.WriteLine($"  Exception: {exc.ToString()}");
                        }

                        if (newObj != null)
                        {
                            var newId = Guid.NewGuid();
                            Objects.TryAdd(newId, new RemotelyCreatedObject { RefCount = 1, Value = newObj });
                            returnVal = newId;
                            Console.WriteLine($"Created a new objet with ID {newId.ToString()} and type {command.Type.FullName}");
                        }
                    }
                    break;
                case Commands.RetrieveObject:
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
                        returnVal = method.Invoke(objectToInvokeOn, parameters);
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
                            returnVal = property.GetValue(objectToAccess);
                        }
                        else if (command.Command == Commands.SetProperty)
                        {
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
