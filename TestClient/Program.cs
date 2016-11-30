using System;
using System.IO.Pipes;
using System.Text;

using RemoteExecution;
using System.Collections.Generic;

namespace TestClient
{
    public class Program
    {
        const string RemoteExecutionName = "MyRemoteServer";
        public static void Main(string[] args)
        {
            Console.WriteLine("Creating RemoteExecutionServer...");

            var remoteServer = new RemoteExecutionServer(RemoteExecutionName);

            //var pipeClient = new NamedPipeClientStream(".", RemoteExecutionName, PipeDirection.InOut, PipeOptions.Asynchronous);
            //pipeClient.Connect();

            //Console.WriteLine("Connection established");

            //while(true)
            //{
            //    Console.Write("Message to send: ");
            //    var bytesToSend = Encoding.UTF8.GetBytes(Console.ReadLine());
            //    pipeClient.WriteAsync(bytesToSend, 0, bytesToSend.Length).Wait();
            //}

            Console.WriteLine("Creating proxy objects...");

            var proxy3 = RemoteExecutionClient.CreateRemoteInstance<List<int>>(RemoteExecutionName);
            Console.WriteLine($"Created remote object with ID: {proxy3.ID}");

            var proxy4 = RemoteExecutionClient.CreateRemoteInstance<RemoteProxy>(RemoteExecutionName, new List<string>(), proxy3.ID);
            Console.WriteLine($"Created remote object with ID: {proxy4.ID}");

            var proxy1 = RemoteExecutionClient.CreateRemoteInstance<string>(RemoteExecutionName);
            Console.WriteLine($"Created remote object with ID: {proxy1.ID}");

            var proxy2 = RemoteExecutionClient.CreateRemoteInstance<Program>(RemoteExecutionName);
            Console.WriteLine($"Created remote object with ID: {proxy2.ID}");
        }
    }
}
