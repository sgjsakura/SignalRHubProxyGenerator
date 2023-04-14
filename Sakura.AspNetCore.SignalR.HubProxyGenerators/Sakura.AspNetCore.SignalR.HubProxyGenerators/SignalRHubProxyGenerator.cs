using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sakura.AspNetCore.SignalR.HubProxies;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators;

/// <summary>
///     Provide ability to generate proxy service class for SignalR Hub.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class SignalRHubProxyGenerator : IIncrementalGenerator
{
	/// <summary>
	///     The full name of the hub type. This field is constant.
	/// </summary>
	private const string HubTypeFullName = "Microsoft.AspNetCore.SignalR.Hub";

	/// <summary>
	///     The full name of the hub type with a strong typed client. This field is constant.
	/// </summary>
	private const string StrongClientHubTypeFullName = "Microsoft.AspNetCore.SignalR.Hub`1";

	/// <inheritdoc />
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Select all attribute with given name

		var data =
			context.CompilationProvider.SelectMany((compilation, _) => compilation.Assembly
				.GetAttributes<HubProxyGenerationAttribute>()
				.Select(i => new { Compilation = compilation, Data = i }));

		context.RegisterSourceOutput(data,
			(productionContext, item) => GenerateProxyForAttribute(productionContext, item.Compilation, item.Data));
	}

	/// <summary>
	///     Generate a proxy type from given HubProxyGenerationAttribute instance.
	/// </summary>
	/// <param name="context">The source production context.</param>
	/// <param name="compilation">The compilation information.</param>
	/// <param name="attr">
	///     The <see cref="HubProxyGenerationAttribute" /> attribute which specifies the hub generation
	///     requirement.
	/// </param>
	/// <exception cref="InvalidOperationException"></exception>
	private void GenerateProxyForAttribute(SourceProductionContext context, Compilation compilation, AttributeData attr)
	{
		try
		{
			var dllPath =
				attr.GetValue<string?>(nameof(HubProxyGenerationAttribute.HubAssemblyPath)) ??
				throw new InvalidOperationException(
					$"the required attribute {nameof(HubProxyGenerationAttribute.HubAssemblyPath)} is not specified and no proxy will be generated.");

			var dllDirectory = Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(dllPath))
			                   ?? throw new InvalidOperationException(
				                   $"Cannot get the parent directory path for dll path \"{dllPath}\", assembly resolving will not work");


			var additionalPaths =
				attr.GetValues<string>(nameof(HubProxyGenerationAttribute.AdditionalAssemblyDirectories));

			var excludedFiles =
				attr.GetValues<string>(nameof(HubProxyGenerationAttribute.ExcludedAssemblyFileNames)).ToArray();

			var assemblyList = new List<string>();


			var validAdditionalAssemblies =
				from path in additionalPaths
				from fileName in Directory.EnumerateFiles(Environment.ExpandEnvironmentVariables(path), "*.dll")
				where !excludedFiles.Contains(Path.GetFileName(fileName), StringComparer.OrdinalIgnoreCase)
				select fileName;

			// Core assembly
			assemblyList.AddRange(Directory.EnumerateFiles(dllDirectory, "*.dll"));

			// Additional assemblies
			assemblyList.AddRange(validAdditionalAssemblies);


			var resolver = new PathAssemblyResolver(assemblyList);
			using var loadContext = new MetadataLoadContext(resolver);

			var asm = loadContext.LoadFromAssemblyPath(dllPath);

			// Create unit as new code root
			var compilationUnit = CompilationUnit();

			// Add to compilation, not the returned value is used for get semantic model only.
			//var compilation = context.Compilation.AddSyntaxTrees(compilationUnit.SyntaxTree);

			compilation = compilation.AddSyntaxTrees(compilationUnit.SyntaxTree);

			// create semantic model
			var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

			var typeList = new List<MemberDeclarationSyntax>();

			foreach (var type in asm.DefinedTypes)
			{
				var hubType = TryLoadBaseHubType(type);

				// A hub must as an hub base type, and cannot be abstract of definitive type.
				if (hubType == null || type.IsAbstract || type.IsGenericTypeDefinition) continue;

				var clientTypeName = string.Format(CultureInfo.InvariantCulture,
					attr.GetValue<string?>(nameof(HubProxyGenerationAttribute
						.TypeNameFormat)) ?? HubProxyGenerationAttribute.DefaultTypeNameFormat, type.Name);
				// Add class
				var classDecl = GenerateProxyForHubType(model, clientTypeName, type, hubType);
				typeList.Add(classDecl);
			}


			// Try get global namespace
			var nsName = attr.GetValue<string?>(nameof(HubProxyGenerationAttribute.RootNamespace));

			if (!string.IsNullOrEmpty(nsName))
			{
				// Root namespace
				var namespaceDecl = GenerateNamespace(nsName!).AddMembers(typeList.ToArray());

				// Add namespace to unit
				compilationUnit = compilationUnit.AddMembers(namespaceDecl);
			}
			else
			{
				compilationUnit = compilationUnit.AddMembers(typeList.ToArray());
			}


			// generate code
			var code = compilationUnit.NormalizeWhitespace().ToFullString();
			context.AddSource(Path.GetFileNameWithoutExtension(dllPath), code);
		}
		catch (Exception ex)
		{
			var message = $"source:{ex.Source}, stack: {ex.StackTrace}, message: {ex.Message}";
			throw new InvalidOperationException(message.Replace("\r", "").Replace("\n", ""));
		}
	}

	/// <summary>
	///     Generate hub client message handlers.
	/// </summary>
	private static ClassDeclarationSyntax GenerateHubClients(SemanticModel model,
		ClassDeclarationSyntax hubClassDeclaration, Type hubType)
	{
		var clientType = GetHubClientType(hubType);

		// Ignore client generation if no client type is detected.
		if (clientType == null) return hubClassDeclaration;

		return new TaskEventClientCallbackGenerator().Generate(model, hubClassDeclaration, clientType);
	}


	/// <summary>
	///     Try get the client type of a hub.
	/// </summary>
	/// <param name="hubType">The hub type.</param>
	/// <returns>The client type of the hub. If no client type can be determined, <c>null</c> will be returned.</returns>
	private static Type? GetHubClientType(Type hubType)
	{
		var currentType = hubType;

		while (currentType != null)
		{
			// The client type is the generic argument of Hub<T>
			if (currentType.IsGenericType &&
			    currentType.GetGenericTypeDefinition().FullName == StrongClientHubTypeFullName)
				return currentType.GenericTypeArguments[0];

			currentType = currentType.BaseType;
		}

		return null;
	}

	/// <summary>
	///     If the type is has a base type represents as the SignalR hub, get the base hub type instance.
	/// </summary>
	/// <param name="type">The actual hub type.</param>
	/// <returns>The type represents as a SignalR hub. If the type is not a hub, returns <c>null</c>.</returns>
	private static Type? TryLoadBaseHubType(Type type)
	{
		var currentType = type;

		while (currentType != null)
		{
			if (currentType.FullName == HubTypeFullName) return currentType;

			currentType = currentType.BaseType;
		}

		return null;
	}

	/// <summary>
	///     Get all the types in the type inheritance tree for a specified type.
	/// </summary>
	/// <param name="type">The <see cref="Type" /> instance.</param>
	/// <returns>The <see cref="Type" /> itself for <paramref name="type" /> and all of its base types.</returns>
	private static IEnumerable<Type> GetTypeAndBases(Type type)
	{
		var currentType = type;

		while (currentType != null)
		{
			yield return currentType;
			currentType = currentType.BaseType;
		}
	}


	private NamespaceDeclarationSyntax GenerateNamespace(string namespaceName)
	{
		var proxyNamespace = NamespaceDeclaration(ParseName(namespaceName));
		return proxyNamespace;
	}

	/// <summary>
	///     Generate proxy type for hub type.
	/// </summary>
	/// <param name="model">The semantic model.</param>
	/// <param name="hubClientTypeName">The name of the hub client type.</param>
	/// <param name="hubType">The hub type.</param>
	/// <param name="hubBaseType">The base type represents as the a SignalR hub.</param>
	/// <returns></returns>
	private static ClassDeclarationSyntax GenerateProxyForHubType(SemanticModel model, string hubClientTypeName,
		Type hubType, Type hubBaseType)
	{
		var baseTypes = GetTypeAndBases(hubBaseType).ToArray();

		bool IsDefinedAtBaseTypes(MemberInfo member)
		{
			return baseTypes.Contains(member.DeclaringType);
		}


		// Create class
		var className = hubClientTypeName;
		var proxyClass = ClassDeclaration(className);

		// HubProxy base class
		proxyClass = proxyClass.WithBaseList(
			SyntaxHelper.BaseList(SimpleBaseType(model.GetTypeSyntax<HubProxy>())));

		// public partial
		proxyClass = proxyClass.WithModifiers(TokenList(Token(PublicKeyword),
			Token(PartialKeyword)));


		var methods = hubType.GetMethods();

		foreach (var method in methods)
		{
			// Must be ordinary instance public method
			if (method.IsStatic || method.IsAbstract || method.IsSpecialName || method.IsConstructor ||
			    !method.IsPublic) continue;

			// Ignore any members defined in the basic hub type.
			if (IsDefinedAtBaseTypes(method)) continue;


			// Add new hub proxy method
			var newMethod = GenerateProxyMethod(model, method);
			if (newMethod != null) proxyClass = proxyClass.AddMembers(newMethod);
		}

		// Hub clients
		proxyClass = GenerateHubClients(model, proxyClass, hubType);

		return proxyClass;
	}

	/// <summary>
	///     Generate hub proxy method.
	/// </summary>
	/// <param name="model"></param>
	/// <param name="hubMethod"></param>
	/// <returns></returns>
	private static MethodDeclarationSyntax? GenerateProxyMethod(SemanticModel model, MethodInfo hubMethod)
	{
		// Not task-like type
		if (!SyntaxHelper.TryGetTaskResultType(hubMethod.ReturnType, out var hubMethodReturnType)) return null;

		// method
		var newMethod =
			MethodDeclaration(
				SyntaxHelper.MakeTaskType(model,
					hubMethodReturnType != null ? model.GetEquivalentType(hubMethodReturnType) : null), hubMethod.Name);

		// public
		newMethod = newMethod.WithModifiers(SyntaxHelper.TokenList(PublicKeyword, AsyncKeyword));

		var methodParameters = hubMethod.GetParameters();

		// add parameters
		foreach (var parameter in methodParameters)
		{
			var newParameter = Parameter(new(), new(),
				model.GetTypeSyntax(parameter.ParameterType), Identifier(parameter.Name), null);

			newMethod = newMethod.AddParameterListParameters(newParameter);
		}

		var cancellationTokenParam = Parameter(Identifier("cancellationToken"))
			.WithType(model.GetTypeSyntax<CancellationToken>())
			.WithDefault(EqualsValueClause(LiteralExpression(DefaultLiteralExpression)));

		newMethod = newMethod.AddParameterListParameters(cancellationTokenParam);


		// body
		newMethod = newMethod.WithBody(GenerateHubProxyMethodBody());

		return newMethod;


		BlockSyntax GenerateHubProxyMethodBody()
		{
			var caller = IdentifierName(nameof(HubProxy.HubConnection));
			var invokeMethod = caller.MemberAccess(nameof(HubConnection.InvokeCoreAsync));


			var initializeList =
				methodParameters.Select(i => IdentifierName(i.Name));

			var argListArg =
				ArrayCreationExpression(
					model.Compilation.ObjectType.ToTypeSyntax(model).ToArrayType(ArrayRankSpecifier()),
					InitializerExpression(ArrayInitializerExpression,
						SeparatedList<ExpressionSyntax>(initializeList)));


			var methodNameArg = LiteralExpression(StringLiteralExpression,
				Literal(hubMethod.Name));

			var returnTypeArg = model.GetTypeSyntax(hubMethodReturnType ?? typeof(void)).TypeOf();

			var cancellationToken = IdentifierName(cancellationTokenParam.Identifier);

			var invokeCall = invokeMethod.Invoke(methodNameArg, returnTypeArg, argListArg, cancellationToken);

			// Cast the result if return type is not void
			if (hubMethodReturnType != null)
				// return (T) await InvokeCore ...
				return invokeCall.Await().Cast(model.GetTypeSyntax(hubMethodReturnType)).Return().AsBlock();
			// await InvokeCore ...
			return invokeCall.Await().AsStatement().AsBlock();
		}
	}

	/// <summary>
	///     Attach Debugger in debug mode. This method is for debugging only purpose.
	/// </summary>
	[Conditional("DEBUG")]
	private static void AttachDebugger()
	{
		if (!Debugger.IsAttached) Debugger.Launch();
	}
}