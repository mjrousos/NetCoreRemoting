using System;
using System.Collections.Generic;

using RemoteExecution;
using TestTypes;
using System.Linq;

// Simple test client to demonstrate remoting functionality with some test input
namespace TestClient
{
    public class Program
    {
        const string RemoteExecutionName = "MyRemoteServer";

        public static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("#########################################");
            Console.WriteLine("# Test 1 - Creating some remote objects #");
            Console.WriteLine("#########################################");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            


            // Test #1
            Console.WriteLine("Creating proxy objects...");

            var proxy0 = RemoteExecutionClient.CreateRemoteInstance<int>(RemoteExecutionName);
            Console.WriteLine($"Created remote object with ID: {proxy0.ID}");

            var proxy1 = RemoteExecutionClient.CreateRemoteInstance<string>(RemoteExecutionName, "Hello, world!".ToCharArray());
            Console.WriteLine($"Created remote object with ID: {proxy1.ID}");

            var proxy2 = RemoteExecutionClient.CreateRemoteInstance<List<double>>(RemoteExecutionName);
            Console.WriteLine($"Created remote object with ID: {proxy2.ID}");

            var proxy3 = RemoteExecutionClient.CreateRemoteInstance<List<int>>(RemoteExecutionName, 5);
            Console.WriteLine($"Created remote object with ID: {proxy3.ID}");
            proxy3.InvokeAsync("Add", 3).Wait();
            proxy3.InvokeAsync("Add", 5).Wait();
            var index = proxy3.InvokeAsync<int>("IndexOf", 5).Result;
            var count = proxy3.GetPropertyAsync<int>("Count").Result;
            Console.WriteLine($"Added items to remote list (count: {count}) and found index of '5' is: {index}");

            var proxy4 = RemoteExecutionClient.CreateRemoteInstance<RemoteProxy>(RemoteExecutionName, typeof(Program), RemoteExecutionName, null);
            Console.WriteLine($"Created remote object with ID: {proxy4.ID}");



            // Test #2
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("###########################################");
            Console.WriteLine("# Test 2 - Interactive remote custom type #");
            Console.WriteLine("###########################################");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();

            var proxy = RemoteExecutionClient.CreateRemoteInstance<MessageHolder>(RemoteExecutionName, "MyMessages");
            Console.WriteLine($"MessageHolder proxy created with ID: {proxy.ID}");
            Console.WriteLine();

            bool shouldQuit = false;

            while (!shouldQuit)
            {
                Console.WriteLine("Select option:");
                Console.WriteLine("  E  -  Enqueue");
                Console.WriteLine("  D  -  Dequeue");
                Console.WriteLine("  L  -  Length");
                Console.WriteLine("  M  -  Retrieve max length");
                Console.WriteLine("  S  -  Set max length");
                Console.WriteLine("  P  -  Print status (remotely)");
                Console.WriteLine("  Q  -  End test");
                Console.Write("  > ");

                var option = char.ToUpperInvariant(Console.ReadLine().FirstOrDefault());

                switch (option)
                {
                    case 'E':
                        Console.Write("Message: ");
                        var message = Console.ReadLine();
                        proxy.InvokeAsync("AddMessageToQueue", message).Wait();
                        Console.WriteLine("Message sent");
                        break;
                    case 'D':
                        Console.WriteLine("Retrieving message...");
                        var retMessage = proxy.InvokeAsync<string>("RetrieveMessageFromQueue").Result;
                        Console.WriteLine($"Message: {retMessage}");
                        break;
                    case 'L':
                        Console.WriteLine("Retrieving length...");
                        var length = proxy.GetPropertyAsync<byte>("Length").Result;
                        Console.WriteLine($"Length: {length}");
                        break;
                    case 'M':
                        Console.WriteLine("Retrieving Max length...");
                        var maxLength = proxy.GetPropertyAsync<byte>("MaxLength").Result;
                        Console.WriteLine($"Max length: {maxLength}");
                        break;
                    case 'S':
                        Console.Write("Max length: ");
                        var newLength = byte.Parse(Console.ReadLine());
                        proxy.SetPropertyAsync("MaxLength", newLength).Wait();
                        Console.WriteLine("Message sent");
                        break;
                    case 'P':
                        Console.WriteLine("Sending log command...");
                        proxy.InvokeAsync("LogQueueToConsole").Wait();
                        Console.WriteLine("Sent");
                        break;
                    case 'Q':
                        shouldQuit = true;
                        break;
                    default:
                        Console.WriteLine("Invalid option");
                        break;
                }
                Console.WriteLine();
            }



            Console.WriteLine("- Test Done -");
        }
    }
}
