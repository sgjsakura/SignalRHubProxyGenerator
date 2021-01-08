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
[assembly: HubProxyGeneration(HubAssemblyPath = "full\path\to\your\web\server\assembly.dll", RootNamespace = "Your.Preferred.Namespace.For.Clients")]
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
var charProxy = new HubConnectionBuilder()
  .WithUrl("http://server.name/hub-name")
  .Build()
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

## Impelementation and Limitations

### Core Type Equivalents

Since your server and client may target on different frameworks, and there are also a dazen of versions for (ASP).NET Core runtime, strict exraction for ASP.NET Core and user defined assemblies or types is neither possible nor appropriate. Instead, the generator uses `FullName` based comparasion for type checking. Under such circumstance, if your assembly contains types with the same full name (type name together with namespaces) as ASP.NET Core hub related types (i.e. you define a type with the full name of `Microsoft.AspNetCore.SignalR.Hub` in your code), or types in any reference assemblies, the process of code detecting and geneartion may be incorrect; in the worst case, your project may be unable to compile. 

Although types with same full name are rare, you may have to change the type name if you actually meet this breaking issue.

### Method and Client Event Generation

#### Server Method Generation

Any type which has the type `Microsoft.AspNetCore.SignalR.Hub` in its base type chain will be considered as a hub type, excepts for abstract of open generic types.

This generator will generate proxy methods for all methods defined on the server hub type (also includes derived methods, if your hub has any a complex heritance chain), while method coming from the base `Hub` type or lower positions will be ignored.

A hub method must be public and non-static, also it have a return type of `Task` or `Task<T>`, otherwise the method will not be considerd as a hub method and will be ignored during the proxy generation. 

#### Client Event Generation

If you hub type derives from `Microsoft.AspNetCore.SignalR.Hub<T>`, thus the generic argument type will be considered as the client contact type, or you may use `HubClientType` in `HubProxyGeneration` attribute to explicitly specify the client type. If the client type is not specified nor be detectable, no client event will be generated.

A hub client method must be public and non-static, and it must returns the non-generic `Task` type (generic versions is not supported by the SignalR runtime). A event with type of `System.Func` will be generated for each method. Although defining events with non-void type conflicts with .NET degisn guideline, it is actually used on events like `Connceted` which are from the SignalR client runtime so we kept this design pattern for client events.

**Note: Currently if your client have methods with too many parameters (larger than 8 or 16, depending on your core version) which causes no matching delegate type in the `System.Func` series can be found, the generation will crash. You may consider merge some parameters into one complex type in order to reduce the parameter count. In future versions we will fix this problem to support long-parameter methods.**

### Intermediate Data Type Supporting

Clients and server transmit data with arguments, thus there must be equivalent types between them. The generator automatically map types with the same full name from the server to the client, and thus all core CLR types (e.g. `System.String`, `System.Int32`, etc.) can be correctly used unless you intentionally define confusing types. 

For user defined complex server data types, there must also be a type with the same full name defined in the client project. We recommends you define a inntermediate assembly which contains all types should be transmitted between server and clients and reference it in both sides to reduce the complexity for type sharing.

**Automatically generating client data types according to server types is not supported in the current version but will be support in the future.**

### Generation Process Controlling

In the current verssion, you may have little control for the generation process. New attributes and options for code generation will be added continously in future versions.

## Future Features List

this project is planned to support the following features in the future:

### Core Logic

- [ ] More reliable assembly and type detection
- [ ] Automatical .NET Core library importing

### Hub Generation

- [ ] Support for long parameterized methods
- [ ] Mapped client type generation for server data types
- [ ] Individually enable/disable code generation for types/methods
- [ ] Server side code generation attributes

### Customization

- [ ] Flexible generated type/member naming rules
- [ ] More client message patterns (using delegates or partial methods, etc.)

### Robustness

- [ ] Automatical renaming for duplicated items
- [ ] Better handling for unsupported/invalid hub members

### Documentation

- [ ] Automatically add documentation for generated members

### Localization

- [ ] Localization for hub generation messages

## Contribution and Issues

If you have any problem or suggestion, please feel free to open new issue. 
