using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RemoteExecution
{
    public static class RemoteExecutionClient
    {
        /// <summary>
        /// Creates a new RemoteProxy for a given type T
        /// </summary>
        /// <typeparam name="T">The type to create remotely</typeparam>
        /// <param name="remoteServerName">The remote execution server's name (for named pipe communication)</param>
        /// <param name="paramaters">Parameters to pass to the type's constructor</param>
        /// <returns>A proxy for managing the remote object</returns>
        public static RemoteProxy CreateRemoteInstance<T>(string remoteServerName, params object[] paramaters)
        {
            return new RemoteProxy(typeof(T), remoteServerName, paramaters);
        }

        /// <summary>
        /// Creates a proxy to an existing remote object
        /// </summary>
        /// <param name="remoteServerName">The remote execution server's name (for named pipe communication)</param>
        /// <param name="objectId">The remote object's ID</param>
        /// <returns>A proxy for managing the remote object</returns>
        public static RemoteProxy GetRemoteInstance(string remoteServerName, Guid objectId)
        {
            return new RemoteProxy(remoteServerName, objectId);
        }
    }
}
