.NET Core Remote Communication and Marshaling
==============================================

Overview
--------

.NET remoting is one of the .NET Framework feature area that is [not available](https://docs.microsoft.com/en-us/dotnet/articles/core/porting/libraries#key-technologies-not-yet-available-on-the-net-standard-or-net-core) in .NET Core.

Although remoting (as it exists in NetFX) is not available, many scenarios that call for remoting can be implemented with alternate approaches in .NET Core. This repo attempts to show a simple sample of that. Note that this repo demonstrates a fairly general approach to the problem. In specific cases, better (and simpler) solutions may be possible which target the goals of that scenario. This repo is just a general-purpose demo of how cross-process managed code invocation can be accomplished in .NET Core.

The required technologies are:
 
* [Named pipes](https://msdn.microsoft.com/en-us/library/bb546085(v=vs.110).aspx) for cross-process communication.
* A serialization technology for marshaling managed objects. In this example, I use [Json.NET](http://www.newtonsoft.com/json). This means that the sample can only send parameters cross-process if their types are serializable/deserializable with Json.NET. All of the serialization logic is confined to a couple classes in the [serialization](https://github.com/mjrousos/NetCoreRemoting/tree/master/RemoteExecution/Serialization) directory, though, so it should be easy to swap out Json.NET for an alternate serialization provider, if needed.
* A proxying technology. In order to keep the sample as simple as possible, it's not using real proxies - I just have a `RemoteProxy` class that takes a string parameter specifying which API to call on a remote object. A more fully-featured remoting story could use something like [Castle.DynamixProxy](https://github.com/castleproject/Core/blob/master/docs/dynamicproxy.md) to create proxy objects for use on the client.

How it Works
------------

As mentioned above, the communication is based on named pipes. To start remoting, the server piece of the scenario must create a [`RemoteExecutionServer`](https://github.com/mjrousos/NetCoreRemoting/blob/master/RemoteExecution/RemoteExecutionServer.cs) object. Doing so will cause a named pipe to be setup for clients to connect to. Once that's done, the server's work is complete.

Then, clients are able to create [`RemoteProxy`](https://github.com/mjrousos/NetCoreRemoting/blob/master/RemoteExecution/RemoteProxy.cs) objects (or use the [`RemoteExecutionClient`](https://github.com/mjrousos/NetCoreRemoting/blob/master/RemoteExecution/RemoteExecutionClient.cs) wrapper class). Creating this type causes the client to connect with the server via named pipes and send a request on that pipe (all requests are instances of [`RemoteCommand`](https://github.com/mjrousos/NetCoreRemoting/blob/master/RemoteExecution/RemoteCommand.cs)) for an instance of the type to be remoted to be created.

On the server side, the necessary type will be created and it will be put in a Dictionary to be referenced later. The key (for looking up the object in the dictionary) is then returned to the `RemoteProxy` where it is stored.

Later, when the client wants to call some API on the remote object, `RemoteProxy.InvokeAsync` (or one of a number of other similar APIs) can be called. This will cause another `RemoteCommand` to be created which will include:

1. The ID of the remote object that the API should be invoked on.
2. The name of the API as a string.
3. The parameters (if any) to pass to the API.
4. The types of those parameters (since they can be lost in serialization).

This information is all serialized and then sent via the named pipe to the server. The server, as might be expected, deserializes the command and uses reflection to invoke the API requested, serializing the return object (if any) to be sent back to the proxy (which, in turn, presents it to its caller).

Trying it Out
-------------

There are some tests in the [Tests](https://github.com/mjrousos/NetCoreRemoting/tree/master/Tests) folder which allow for trying out the remoting mentioned here. To experiment with it, follow these steps:

1. Run the [TestServer](https://github.com/mjrousos/NetCoreRemoting/tree/master/Tests/TestServer) application to start listening for remote objects.
	1. Before running, though, make sure that you've copied TestClient and TestComponent dlls next to the TestServer app. This is important because TestServer doesn't usually depend on those libraries, but the client will be requesting that types from them be created. Therefore, it's necessary for TestServer to be able to find the assemblies so that it can load and activate the required types.
2. Run the [TestClient](https://github.com/mjrousos/NetCoreRemoting/tree/master/Tests/TestClient) which will first make a series of hard-coded remote calls that I found useful for initial ad-hoc testing, and then create a remote instance of [`TestTypes.MessageHolder`](https://github.com/mjrousos/NetCoreRemoting/blob/master/Tests/TestComponent/TestType.cs) (a glorified wrapper around a queue) and allow the user to ask for different `MessageHolder` commands to be executed remotely. Of special note is the 'print' command which will cause information about the queue to be written to the console. When calling this, notice that the console output happens in the TestServer command prompt since that's the process in which all the `MessageHolder` code is executing.  

Demo
----

There's a brief demo video of this sample available [on YouTube](https://youtu.be/QwvYXrHM4E4).