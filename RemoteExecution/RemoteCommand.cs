using System;

namespace RemoteExecution
{
    internal class RemoteCommand
    {
        // Common properties
        public int CommandVersion { get; set; } = 1;
        public Commands Command { get; set; }
        
        // Used for object retrieval or instance method invocation
        public Guid ObjectId { get; set; }

        // Used for method invocation
        public string MethodName { get; set; }

        // Used for method invocation or object creation
        public object[] Parameters { get; set; }

        // Newtonsoft.Json does not persist type information for
        // some primitive types, so that information needs to be tracked explicitly
        // for parameters.
        public Type[] ParameterTypes { get; set; }

        // Used for new object creation or static method execution
        public Type Type { get; set; }
    }
}