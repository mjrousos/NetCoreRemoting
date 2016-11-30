using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RemoteExecution
{
    public static class RemoteExecutionClient
    {
        public static RemoteProxy CreateRemoteInstance<T>(string remoteServerName, params object[] paramaters)
        {
            return new RemoteProxy(typeof(T), remoteServerName, paramaters);
        }

        public static RemoteProxy GetRemoteInstance(string remoteServerName, Guid objectId)
        {
            return new RemoteProxy(remoteServerName, objectId);
        }
    }
}
