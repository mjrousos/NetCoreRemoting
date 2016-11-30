using System;
using System.IO.Pipes;
using System.Text;

using RemoteExecution;

namespace TestClient
{
    public class Program
    {
        const string RemoteExecutionName = "MyRemoteServer";
        public static void Main(string[] args)
        {
            Console.WriteLine("Creating RemoteExecutionServer...");

            var remoteServer = new RemoteExecutionServer(RemoteExecutionName);

            var pipeClient = new NamedPipeClientStream(".", RemoteExecutionName, PipeDirection.InOut, PipeOptions.Asynchronous);
            pipeClient.Connect();

            Console.WriteLine("Connection established");

            while(true)
            {
                Console.Write("Message to send: ");
                var bytesToSend = Encoding.UTF8.GetBytes(Console.ReadLine());
                pipeClient.WriteAsync(bytesToSend, 0, bytesToSend.Length).Wait();
            }
        }
    }
}
