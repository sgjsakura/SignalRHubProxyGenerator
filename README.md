# ASP.NET Core SignalR Strong-Typed Hub Proxy Generator

## Background

ASP.NET Core SignalR is a excellent successor for original ASP.NET SignalR framework, it provides fast, low latency and reliable duplex communication between web server and various clients (including Windows desktop, Web page, etc.).

SignalR uses framework-independent serliazation to provide data tarnsfer compabitlity between different client development environments, which introduce a major side effect that developers must implements all intermediate types and action calls manually. The original ASP.NET SignalR can generate a Javascript proxy script file in order to simplify the client development with strong-typed method calls and data type definitions. However in the new .NET Core version, this feature is removed; also, it provides little help for other clients rather than Javascript ones (e.g. clients using C#).

You may using shared libraries to share data types beteween client and server project in order to simplify data type definition work, while sometimes the source code of the server is not under your control and thus you cannot share codes. And what's more, the interface and action calls definition is ever not sharable since they are business types with heavy logic code written.

This project tries to take the advantage from the newly introducec C# source generators features to simplify the SignalR hub client design process for .NET Standard project using C# 7.3 or later. The source generator will parse the hub type information on the server and then generate easy-to-use proxy types for them. Drop the borning code copying off and just enjoy focusing on your core business! 

## How to Use

### Installation

To generate SignalR strong-typed client hub proxies, you should take the following steps:

1. Add a package reference of `Sakura.AspNetCore.SignalR.HubProxies`, which provide necessary common logics for hub proxies and generation controlling attributes.

2. Add a package reference of `Sakura.AspNetCore.SignalR.HubProxyGenerators`, this is a source generator package and thus after use installed it, it will be listed in the `Analyzers` nodes in your project.

3. Provide necessary information for hub proxy generations, you should at least take 2 steps in order to make generating process works:

- Add a `NetCoreRuntimeLocation` attribute to specify the install location of ASP.NET Core shared framework libraries, this is important since the generation engine must load them to detect SignalR Hub related type information. Usually they will be located at `C:\Program Files\dotnet\packs`, thus you may set the attribute like:
```C#
[assembly: NetCoreEnviromentLocation(@"C:\Program Files\dotnet\packs")]
```

- Add a `HubProxyGeneration` attribute to locate the library which contains the hub server type and control various generating settings, an example may be:
```C#
[assembly: HubProxyGeneration(HubAssemblyPath = "full\path\to\my\web\server\assembly.dll", RootNamespace = "Your.Preferred.Namespace.For.Clients")]
```

Now you may build you project and Visual Studio will automatically call the source generator to generate hub client types.

### Using Strong-Typed Hub Proxy Class

After the hub client is generated, you may take benefit in SignalR hub related codes. Assuming you have a hub defined in server as the following:

```C#
public classd ChatHub : Hub<IMyHubClient>
{
  public Task<bool> SendMessage(string user, string content) { /* ... */ } 
}

public interface IMyHubClient
{
  Task MessageReceived(string user, string content);
}
```

The generated SignalR Hub proxy will just like:
```C#
public class ChatHubProxy
{
  public Task<bool> SendMessage(string user, string content) { /* ... */ }
  public event Func<string, string, Task> MessageReceived;
}
```

To use the strong-typed hub client, first call `CreateProxy` extension method on an existing `HubConnection` instance as following:
```C#
var charProxy = new HubConnectionBuilder.WithUrl("http://server.name/hub-name").Build()
  .CreateProxy<ChartHubProxy>(); // new extension method used to create hub proxies.
```

The hub proxy has the same method of `Start` and `Stop` as `HubConnection` and you may use them to control the connectivity of the hub. It also exposed a property named `HubConnection`  for low level access, and it may be useful if you need more complex management on hub connections.

To call a hub method and send message to server, just call the method defined on proxy as the following:
```C#
var result = await chatProxy.SendMessage("Alice", "Hello.");
```

To handle incoming message from server, use the event defined in the proxy just like:
```
chatProxy.MessageReceived += async (user, message) => { Console.WriteLine("{0} says: {1}", user, message) };
```
*Note: You may add and remove mesasge handlers at any time (while original `HubConnection` class requires you subscribe messages before it starts).*
