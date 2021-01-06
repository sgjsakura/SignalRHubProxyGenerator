using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Sakura.AspNetCore.SignalR.HubProxies;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators
{
	/// <summary>
	/// Provide ability to generate proxy service class for SignalR Hub.
	/// </summary>
	[Generator]
	public class SignalRHubProxyGenerator : ISourceGenerator, IDisposable
	{
		/// <summary>
		/// The <see cref="MetadataLoadContext"/> service instance used to load assemblies.
		/// </summary>
		private MetadataLoadContext LoadContext { get; set; } = null!;

		/// <summary>
		/// The full name of the hub type. This field is constant.
		/// </summary>
		private const string HubTypeFullName = "Microsoft.AspNetCore.SignalR.Hub";

		/// <summary>
		/// The full name of the hub type with a strong typed client. This field is constant.
		/// </summary>

		private const string StrongClientHubTypeFullName = "Microsoft.AspNetCore.SignalR.Hub`1";

		/// <summary>
		/// Load a DLL file into the load context.
		/// </summary>
		/// <param name="dllPath">The file path for the library.</param>
		/// <returns>The loaded <see cref="Assembly"/> object.</returns>
		private Assembly LoadDll(string dllPath)
		{
			var asm = LoadContext.LoadFromAssemblyPath(dllPath);
			return asm;
		}

		/// <inheritdoc />
		public void Execute(GeneratorExecutionContext context)
		{
			AttachDebugger();
			var assemblyParsed = false;

			foreach (var syntaxTree in context.Compilation.SyntaxTrees)
			{
				if (assemblyParsed)
				{
					continue;
				}

				assemblyParsed = true;

				var model = context.Compilation.GetSemanticModel(syntaxTree);

				var generationAttributeData = model.Compilation.Assembly
					.GetAttributes()
					.FirstOrDefault(i => i.AttributeClass?.Name == nameof(HubProxyGenerationAttribute));

				// No generation specified
				if (generationAttributeData == null)
				{
					return;
				}


				var dllPath =
					generationAttributeData.GetValue<string>(nameof(HubProxyGenerationAttribute.HubAssemblyPath));

				var asm = LoadDll(dllPath);

				var namespaceDecl = GenerateNamespace(generationAttributeData.GetValue<string>(nameof(HubProxyGenerationAttribute.RootNamespace)));

				foreach (var type in asm.DefinedTypes)
				{
					var hubType = TryLoadBaseHubType(type);

					// A hub must as an hub base type, and cannot be abstract of definitive type.
					if (hubType == null || type.IsAbstract || type.IsGenericTypeDefinition)
					{
						continue;
					}

					var clientTypeName = string.Format(CultureInfo.InvariantCulture,
						generationAttributeData.GetValue<string>(nameof(HubProxyGenerationAttribute
							.TypeNameFormat)) ?? HubProxyGenerationAttribute.DefaultTypeNameFormat, type.Name);
					// Add class
					var classDecl = GenerateProxyForHubType(model, clientTypeName, type, hubType);
					namespaceDecl = namespaceDecl.AddMembers(classDecl);
				}


				var code = namespaceDecl.NormalizeWhitespace().ToFullString();
				Debug.WriteLine(code);
				context.AddSource("HubProxy.cs", code);
			}
		}

		/// <summary>
		/// Generate hub client message handlers.
		/// </summary>
		private static ClassDeclarationSyntax GenerateHubClients(SemanticModel model, ClassDeclarationSyntax hubClassDeclaration, Type hubType)
		{
			var clientType = GetHubClientType(hubType);

			// Ignore client generation if no client type is detected.
			if (clientType == null)
			{
				return hubClassDeclaration;
			}

			return new TaskEventClientCallbackGenerator().Generate(model, hubClassDeclaration, clientType);
		}


		/// <summary>
		/// Try get the client type of a hub.
		/// </summary>
		/// <param name="hubType">The hub type.</param>
		/// <returns>The client type of the hub. If no client type can be determined, <c>null</c> will be returned.</returns>
		private static Type? GetHubClientType(Type hubType)
		{
			var currentType = hubType;

			while (currentType != null)
			{
				// The client type is the generic argument of Hub<T>
				if (currentType.IsGenericType && currentType.GetGenericTypeDefinition().FullName == StrongClientHubTypeFullName)
				{
					return currentType.GenericTypeArguments[0];
				}

				currentType = currentType.BaseType;
			}

			return null;
		}

		/// <summary>
		/// If the type is has a base type represents as the SignalR hub, get the base hub type instance.
		/// </summary>
		/// <param name="type">The actual hub type.</param>
		/// <returns>The type represents as a SignalR hub. If the type is not a hub, returns <c>null</c>.</returns>
		private static Type? TryLoadBaseHubType(Type type)
		{
			var currentType = type;

			while (currentType != null)
			{
				if (currentType.FullName == HubTypeFullName)
				{
					return currentType;
				}

				currentType = currentType.BaseType;
			}

			return null;
		}

		/// <summary>
		/// Get all the types in the type inheritance tree for a specified type.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> instance.</param>
		/// <returns>The <see cref="Type"/> itself for <paramref name="type"/> and all of its base types.</returns>
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
		/// Generate proxy type for hub type.
		/// </summary>
		/// <param name="model">The semantic model.</param>
		/// <param name="hubClientTypeName">The name of the hub client type.</param>
		/// <param name="hubType">The hub type.</param>
		/// <param name="hubBaseType">The base type represents as the a SignalR hub.</param>
		/// <returns></returns>
		private static ClassDeclarationSyntax GenerateProxyForHubType(SemanticModel model, string hubClientTypeName, Type hubType, Type hubBaseType)
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
				if (method.IsStatic || method.IsAbstract || method.IsSpecialName || method.IsConstructor || !method.IsPublic)
				{
					continue;
				}

				// Ignore any members defined in the basic hub type.
				if (IsDefinedAtBaseTypes(method))
				{
					continue;
				}

				// Add new hub proxy method
				var newMethod = GenerateProxyMethod(model, method);
				if (newMethod != null)
				{
					proxyClass = proxyClass.AddMembers(newMethod);
				}
			}

			// Hub clients
			proxyClass = GenerateHubClients(model, proxyClass, hubType);

			return proxyClass;
		}

		/// <summary>
		/// Generate hub proxy method.
		/// </summary>
		/// <param name="model"></param>
		/// <param name="hubMethod"></param>
		/// <returns></returns>
		private static MethodDeclarationSyntax? GenerateProxyMethod(SemanticModel model, MethodInfo hubMethod)
		{
			// Not task-like type
			if (!SyntaxHelper.TryGetTaskResultType(hubMethod.ReturnType, out var hubMethodReturnType))
			{
				return null;
			}

			// method
			var newMethod = MethodDeclaration(SyntaxHelper.MakeTaskType(model, hubMethodReturnType != null ? model.GetEquivalentType(hubMethodReturnType) : null), hubMethod.Name);

			// public
			newMethod = newMethod.WithModifiers(SyntaxHelper.TokenList(PublicKeyword, AsyncKeyword));

			var methodParameters = hubMethod.GetParameters();

			// add parameters
			foreach (var parameter in methodParameters)
			{
				var newParameter = Parameter(new SyntaxList<AttributeListSyntax>(), new SyntaxTokenList(),
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
				{
					// return (T) await InvokeCore ...
					return invokeCall.Await().Cast(model.GetTypeSyntax(hubMethodReturnType)).Return().AsBlock();
				}
				else
				{
					// await InvokeCore ...
					return invokeCall.Await().AsStatement().AsBlock();
				}
				
			}
		}


		/// <inheritdoc />
		public void Initialize(GeneratorInitializationContext context)
		{

			var libs = new List<string>();

			var runtimeLibs = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
			var coreLibs =
				Directory.GetFiles(
					Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "packs"),
					"*.dll", SearchOption.AllDirectories);

			libs.AddRange(runtimeLibs);
			libs.AddRange(coreLibs.Where(i => Path.GetFileName(i) != "mscorlib.dll"));

			var resolver = new PathAssemblyResolver(libs.ToArray());

			LoadContext = new MetadataLoadContext(resolver);
		}

		/// <summary>
		/// Attach Debugger in debug mode. This method is for debugging only purpose.
		/// </summary>
		[Conditional("DEBUG")]
		private static void AttachDebugger()
		{
			if (!Debugger.IsAttached)
			{
				//Debugger.Launch();
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			LoadContext?.Dispose();
		}
	}
}
