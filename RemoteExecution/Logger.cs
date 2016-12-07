using System;
using System.Text;

namespace RemoteExecution
{
    public class Logger
    {
        object LoggingLock = new object();

        public MessagePriority LogLevel { get; set; } = MessagePriority.Informational;

        public void Log(string message, MessagePriority priority = MessagePriority.Verbose)
        {
            if (priority <= LogLevel)
            {
                lock (LoggingLock)
                {
                    ConsoleColor previousForeColor = Console.ForegroundColor;
                    StringBuilder toWrite = new StringBuilder($"[{DateTime.Now.ToString("HH:MM:ss.ff")}] ");

                    switch (priority)
                    {
                        case MessagePriority.Error:
                            Console.ForegroundColor = ConsoleColor.Red;
                            toWrite.Append("[ERROR] ");
                            break;
                        case MessagePriority.Warning:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            toWrite.Append("[Warning] ");
                            break;
                        case MessagePriority.Verbose:
                            Console.ForegroundColor = ConsoleColor.Gray;
                            break;
                        case MessagePriority.Informational:
                            Console.ForegroundColor = ConsoleColor.White;
                            break;
                    }

                    toWrite.Append(message);

                    Console.WriteLine(toWrite.ToString());
                    Console.ForegroundColor = previousForeColor;
                }
            }
        }
    }

    public enum MessagePriority
    {
        Error,
        Warning,
        Informational,
        Verbose
    }
}
