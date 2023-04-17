# ASP.NET Core SignalR Strong-Typed Hub Proxy Generator

## Background

[ASP.NET Core SignalR](https://dotnet.microsoft.com/apps/aspnet/signalr) is an excellent successor for original ASP.NET SignalR framework, it provides fast, low latency and reliable duplex communication between web server and various clients, including Windows desktop, Web page, etc.

SignalR uses framework-independent serliazation to provide data tarnsfer compatbility for different client development environments, which introduce a major side effect that developers must implements all intermediate types and action calls manually. The original ASP.NET SignalR can generate a Javascript proxy script file in order to simplify the Javascript client development, while in the new .NET Core version, this feature is removed; also, it provides little help for other clients rather than Javascript ones (e.g. for clients using C#).

You may use shared libraries to share data types beteween client and server projects in order to simplify data type definition work, however, sometimes the source code of the server is not under your control and thus you cannot share codes. And what's more, the interface and action calls is never sharable, since they are business types with heavy logic code inside.

This project tries to take the advantage from the newly introduced C# source generator feature, in order to simplify the SignalR hub client design process for .NET Standard project using C# 7.3 or later. It will parse the hub type information at server and generate easy-to-use proxy types for them at your client. Drop the borning code copying off and just enjoy focusing on your core business! 

## How to Use

### Installation

To generate SignalR strong-typed client hub proxies, you should take the following steps:

1. Add a package reference of [`Sakura.AspNetCore.SignalR.HubProxies`](https://www.nuget.org/packages/Sakura.AspNetCore.SignalR.HubProxies/), which provide necessary common logics for hub proxies and generation controlling attributes.

2. Add a package reference of [`Sakura.AspNetCore.SignalR.HubProxyGenerators`](https://www.nuget.org/packages/Sakura.AspNetCore.SignalR.HubProxyGenerators), this is a source generator package and thus it will be listed in the `Analyzers` nodes in your project after you installed it.

3. Provide necessary information for hub proxy generation:

- Add a `HubProxyGeneration` attribute to locate the library which contains the hub server type and control various generating settings, an example may be:
```C#
[assembly: HubProxyGeneration(HubAssemblyPath = "full\path\to\your\web\server\assembly.dll", RootNamespace = "Your.Preferred.Namespace.For.Clients", AdditionalAssemblyDirectories = new []{@"%ProgramFiles%\dotnet\shared\Microsoft.NETCore.App\7.0.0", @"%ProgramFiles%\dotnet\shared\Microsoft.AspNetCore.App\7.0.0")]
```
Some important things you must know for this step:
- You must specify the full path of the assembly dll, relative path will never work, this is because source generators is designed as no project file access permission, and thus the working directory for them will always be the directory of the C# compiler aka the `csc.exe`.
- All the asssemblies located at the same directory of the target assembly will be automatically imported for analyzing, however, typical ASP.NET Core web apps will not copy the common runtime assemblies of .NET core and ASP.NET because it assumes the deployment environment has already installed the runtime globally (unless you select to use the `standalone` publish mode for your project). Under such circumstance, you must specify the install location for all the shared runtime assemblies using the `AdditionalAssemblyDirectories` attribute property.
- Multiple versions of .NET runtime can be side-by-side installed in the same system, so you must select the correct directory which matching the version of your target assembly, otherwise, the loading process cannot run successfully.

4. Now you may build you project and Visual Studio will automatically call the source generator to generate hub client types.

### Using Strong-Typed Hub Proxy Class

After the hub client is generated, you may take benefit in SignalR hub related codes. Assuming you have a hub defined in server as the following:

```C#
public class ChatHub : Hub<IMyHubClient>
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
```C#
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

This generator will generate proxy methods for all methods defined on the server hub type (also includes derived ones if your hub has a complex inheritance chain), while methods coming from the base `Hub` type or lower inheritance positions will be ignored.

A hub method must be public and non-static, also it have a return type of `Task` or `Task<T>`, otherwise the method will not be considerd as a hub method and will be ignored during the proxy generation. 

#### Client Event Generation

If your hub type derives from `Microsoft.AspNetCore.SignalR.Hub<T>`, the generic argument type will be considered as the client contract type, or you may use `HubClientType` property in the `HubProxyGeneration` attribute instance to explicitly specify the client type. If the client type is not specified nor be detectable, no client event will be generated.

A hub client method must be public and non-static, and it must returns the non-generic `Task` type (generic versions is not supported by the SignalR runtime). An event with type from `System.Func` series will be generated for each method. Although defining events with non-void type conflicts with .NET degisn guideline, it is actually used on events like `Connected` which are from the SignalR client runtime so we kept this design pattern for client events.

**Note: Currently if your client have a method with too many parameters (larger than 8 or 16, depending on your core version) which causes no matching delegate type in the `System.Func` series can be found, the generation will crash. You may consider to merge some parameters as one complex type in order to reduce the parameter count. In future versions we will fix this problem to support long-parameterized methods.**

### Intermediate Data Type Supporting

Clients and server transmit data with arguments, thus there must be equivalent types between them. The generator automatically map types with the same full name from the server to the client, and thus all core CLR types (e.g. `System.String`, `System.Int32`, etc.) can be correctly selected unless you intentionally define confusing types. 

For user defined complex server data types, there must also be a type with the same full name defined in the client project. We suggest you define an intermediate assembly which contains all types should be transmitted between server and clients and reference it in both sides to reduce the complexity of type sharing.

**Automatically generates client data types according to server types is not supported in the current version but will be support in the future.**

### Generation Process Controlling

In the current version, you may have little control for the generation process. New attributes and options for code generation will be added continuously in future versions.

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

## Troubleshooting


**Generation aborted and a warning message occured during the building**

It means exception raised during the generation process. You may read the message and try to fix it by adjust generation and project settings. You are welcome to submit an issue to report any generation errors.

**The building process finished successfully but nothing happens, no proxy type can be used in my project**

Since the source generator is a new feature just introduced in the lastest version of Visual Studio, there is still quite stable for use. Sometimes the generated code cannot be detected by the editor and thus you cannot found or use them. You may restart Visual Studio after your project builded and you may found the new generated proxy types. If you have tried many time but you still fail, you may submit issues here or report to the [.NET Roslyn developement team](https://github.com/dotnet/roslyn). 

## Contribution and Issues

If you have any problem or suggestion, please feel free to open new issue. 
