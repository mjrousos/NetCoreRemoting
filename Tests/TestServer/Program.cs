using System;

using RemoteExecution;

namespace TestServer
{
    public class Program
    {
        const string RemoteExecutionName = "MyRemoteServer";
        static RemoteExecutionServer RemoteServer;

        public static void Main(string[] args)
        {
            Console.WriteLine("Creating RemoteExecutionServer...");

            RemoteServer = new RemoteExecutionServer(RemoteExecutionName);

            // Enable verbose logging
            RemoteServer.Logger.LogLevel = MessagePriority.Verbose;

            // Report server creation and wait
            Console.WriteLine($"Remote server created with name {RemoteServer.RemoteName}");
            Console.WriteLine("Press enter to quit");
            Console.WriteLine();
            Console.ReadLine();
        }
    }
}
