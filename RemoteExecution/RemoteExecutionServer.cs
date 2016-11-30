using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
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
                        var buffer = new byte[1024];
                        var bytesRead = await pipe.ReadAsync(buffer, 0, buffer.Length, CTS.Token);
                        messageBytes.AddRange(buffer.Take(bytesRead));
                    }
                    while (!pipe.IsMessageComplete && !CTS.IsCancellationRequested);

                    if (pipe.IsMessageComplete && messageBytes.Count > 0)
                    {
                        // TODO : Process message
                        Console.WriteLine($"Received {messageBytes.Count} bytes:\n{string.Join(" ", messageBytes.Select(b => b.ToString("X2")))}");
                        Console.WriteLine();
                    }
                }
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
