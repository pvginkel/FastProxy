# FastProxy

FastProxy is a simple, fast, .NET TCP/IP proxy server.

The primary use case for FastProxy is for use in unit test or load test applications that test applications or components that expose services over the internet.

[Install from NuGet](https://www.nuget.org/packages/FastProxy).

[API Documentation](https://pvginkel.github.io/FastProxy/).

## Introduction

FastProxy is a simple proxy server that allows you to get control over network connections between a server and a client. When building applications that e.g. need to be resilient against connection drops, it's very difficult to simulate this. FastProxy gives you the tools you need to properly test this and make your applications resilient against e.g. connection drops.

The proxy server is built to be as fast as possible, to ensure it has as little impact on e.g. load tests as possible. Without any custom listeners configured, it will not allocation any memory after a connection has been established. The load testers are able to reach a 500 Mb/s data transfer speed tested on a 4 core Xeon server. Internally it uses [SocketAsyncEventArgs](https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socketasynceventargs) and lock free multithreading to make the proxy server as high performance and low impact as possible without requiring any external dependencies or unsafe code.

FastProxy has been tested on .NET Framework and .NET Core 2.2 and .NET Core 3.0 preview 7.

## Usage

The FastProxy project exposes a `ProxyServer` class. This class implements the .NET proxy that you would put between your server and client. The most trivial use case is as follows:

```cs
public static void ProxyEcho()
{
    using (var echoServer = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
    {
        echoServer.Start();

        var connector = new SimpleConnector(echoServer.EndPoint);

        using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
        {
            proxyServer.Start();

            var block = Encoding.UTF8.GetBytes("Hello world!");

            using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
            {
                echoClient.Start();

                echoClient.Ping();
            }
        }
    }
}
```

The `EchoServer` and `EchoPingClient` are a simple server and client implementation used in the FastProxy project for testing purposes. The `EchoServer` simply echoes back everything it receives, and the `EchoPingClient` sends out data and waits for it to be returned.

A `ProxyServer` instance requires the following inputs:

* An `IPEndPoint` at which to run the server. The above example specifies `0` as the port, which means a port will be chosen at random. The `EndPoint` property can be used to retrieve this port;
* An `IConnector` to configure incoming connections.

The above example uses the `SimpleConnector` implementation to use a fixed `IPEndPoint` and optional `IListener`. The `IPEndPoint` provided is used by the proxy server to proxy the incoming connection to. The `SimpleConnector` class takes an optional `IListener` used to integrate with the proxy server. There are a number of stock listeners in the project, or you can create your own.

## Examples

The section below has a number of samples on what you can do with FastProxy.

### Load balancing

The `IConnector` allows you to integrate with the `ProxyServer` to control what happens when a new connection is established. One of the parameters you need to return is the `IPEndPoint` specifying the address of the upstream server. A simple use case for this is load balancing.

```cs
public static void LoadBalancedProxyEcho()
{
    using (var echoServer1 = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
    using (var echoServer2 = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
    using (var echoServer3 = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
    using (var echoServer4 = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
    {
        echoServer1.Start();
        echoServer2.Start();
        echoServer3.Start();
        echoServer4.Start();

        var connector = new RoundRobinLoadBalancingConnector(
            echoServer1.EndPoint,
            echoServer2.EndPoint,
            echoServer3.EndPoint,
            echoServer4.EndPoint
        );

        using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
        {
            proxyServer.Start();

            var block = Encoding.UTF8.GetBytes("Hello world!");

            for (int i = 0; i < 32; i++)
            {
                using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
                {
                    echoClient.Start();

                    echoClient.Ping();
                }
            }
        }
    }
}

public class RoundRobinLoadBalancingConnector : IConnector
{
    private readonly IPEndPoint[] endpoints;
    private int nextEndpoint;

    public RoundRobinLoadBalancingConnector(params IPEndPoint[] endpoints)
    {
        this.endpoints = endpoints;
    }

    public ConnectResult Connect(out IPEndPoint endpoint, out IListener listener)
    {
        int nextEndpoint = Interlocked.Increment(ref this.nextEndpoint);

        endpoint = endpoints[nextEndpoint % endpoints.Length];
        listener = null;

        return ConnectResult.Accept;
    }
}
```

The above example is similar to the `EchoServer` example at the top, except that it starts four servers. Then, a customer `IConnector` implementation is used to control which of the servers to pick when accepting an incoming connection.

The `RoundRobinLoadBalancingConnector` implementation above takes an array of endpoints (the ones of the different servers), and every time a new connection is made, the next one is used. This ensures that every new incoming connection connects to a different upstream server in a round robin fashion.

### Bandwidth throttling

The FastProxy project contains a number of stock listeners. The example below uses the bandwidth throttling listener.

```cs
public static void BandwidthThrottlingProxyEcho()
{
    using (var echoServer = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
    {
        echoServer.Start();

        var listener = new ThrottlingListener(
            SinkListener.Instance,
            10 * 1024 /* 10 Kb/s */
        );
        var connector = new SimpleConnector(echoServer.EndPoint, listener);

        using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
        {
            proxyServer.Start();

            var block = Encoding.UTF8.GetBytes(new string('?', 1024));

            var stopwatch = Stopwatch.StartNew();

            using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
            {
                echoClient.Start();

                for (int i = 0; i < 100; i++)
                {
                    echoClient.Ping();
                }
            }

            Console.WriteLine(stopwatch.Elapsed);
        }
    }
}
```

This example configures a `ThrottlingListener` to throttle connections at 10 Kb/s. Note that the listener requires another listener as the first parameter. All stock listeners follow this pattern, allowing you to chain multiple listeners together.

The data block transferred by the echo client is 1 Kb in size. With the bandwidth throttled to 10 Kb/s, the console output will show an elapsed time of roughly 10 seconds.

To implement connection throttling, the `ThrottlingListener` uses the `OperationContinuation` class. Whenever data is sent to or received from the client or server, the `IListener.DataReceived` method is called with the number of bytes received from either end, and the direction in which the data is flowing. This method expects an `OperationResult`. The `DataReceived` method can return one of three values:

* `OperationResult.Continue`: Allow the data to be forwarded to the server or client;
* `OperationResult.CloseClient`: Abort the connection. This is used by the `ChaosConnector` to simulate network errors;
* `OperationContinuation.Result`: A pending result to be completed later.

The last option is used to allow the `OperationResult` outcome to be decided at a later time.

The `ThrottlingListener` uses this as follows:

* A budget is calculated for how much data can be received within a specific timeframe. By default, this is calculated per 100 ms;
* If within a timeframe the budget is exceeded, the `ThrottlingListener` will return `OperationContinuation.Result`'s, and store the `OperationContinuation` instances in a list;
* Then, when a timer expires, the budget is reset and all stored `OperationContinuation`'s will have their outcome set, allowing the data to be forwarded to the server or client.

### Delaying the outcome of an operation

The above example describes the working of the `OperationContinuation` class. The example below uses this to delay transfer for a set time:

```cs
public static void DelayTransferEchoServer()
{
    using (var echoServer = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
    {
        echoServer.Start();

        var listener = new FixedDelayListener(
            SinkListener.Instance,
            TimeSpan.FromSeconds(0.5)
        );
        var connector = new SimpleConnector(echoServer.EndPoint, listener);

        using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
        {
            proxyServer.Start();

            var block = Encoding.UTF8.GetBytes("Hello world!");

            using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
            {
                echoClient.Start();

                var stopwatch = Stopwatch.StartNew();

                echoClient.Ping();

                Console.WriteLine(stopwatch.Elapsed);
            }
        }
    }
}

public class FixedDelayListener : DelegatingListener
{
    private readonly TimeSpan delay;

    public FixedDelayListener(IListener inner, TimeSpan delay)
        : base(inner)
    {
        this.delay = delay;
    }

    public override OperationResult DataReceived(int bytesTransferred, Direction direction)
    {
        // Call the base listener and return that value if it's not continue.

        var result = base.DataReceived(bytesTransferred, direction);
        if (result.Outcome != OperationOutcome.Continue)
            return result;

        // Create an operation continuation and schedule it to be ran after
        // some time.

        var continuation = new OperationContinuation();

        Task.Run(async () =>
        {
            await Task.Delay(delay);

            continuation.SetOutcome(OperationOutcome.Continue);
        });

        return continuation.Result;
    }
}
```

The `FixedDelayListener` example above implements a listener that will delay every data transfer for some time.

This class inherits from `DelegatingListener`. This class implements some patterns to properly structure your listener and should be used as a base class for any listener.

The implementation of the `DataReceived` method does the following:

* The base class implementation is called. If this returns a result with an outcome other than `OperationOutcome.Continue`, that result is returned immediately. This ensures that you play nice with any other configured listeners;
* Then, an `OperationContinuation` is instantiated. The `Result` property provides you with an `OperationResult` that's configured to complete once you set the outcome of the `OperationContinuation`. This is returned from the method;
* In parallel, a timer is started that will, after a configured time interval, sets the outcome of the `OperationContinuation` to `OperationOutcome.Continue`.

If you run this example, the console will show an elapsed time close to one second (twice the configured delay, since the data is echoed back from the server).