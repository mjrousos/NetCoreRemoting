using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteExecution
{
    public class RemoteExecutionServer : IDisposable
    {
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
                Console.WriteLine($"\tMethod name: {command.MethodName}");
                Console.WriteLine($"\tParameter cound: {command.Parameters?.Length ?? 0}");
                Console.WriteLine($"\tType: {command.Type.AssemblyQualifiedName}");
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
                    // TODO - Decrement ref count on associated object
                    shouldCloseConnection = true;
                    break;

                // TODO
                case Commands.NewObject:

                    // TODO : TEMPORARY!
                    returnVal = Guid.NewGuid();

                    break;
                case Commands.RetrieveObject:
                    break;
                case Commands.Invoke:
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
