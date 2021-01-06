using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Sakura.AspNetCore.SignalR.HubProxies;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using static Sakura.AspNetCore.SignalR.HubProxyGenerators.SyntaxHelper;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators
{
	/// <summary>
	/// Generate client callback message handlers with task-based delegates.
	/// </summary>
	public class TaskEventClientCallbackGenerator : ClientCallbackGenerator
	{
		public override ClassDeclarationSyntax Generate(SemanticModel model, ClassDeclarationSyntax classDeclaration, Type hubClientType)
		{
			var bindingStatements = new List<StatementSyntax>();

			foreach (var method in hubClientType.GetTypeInfo().DeclaredMethods)
			{
				// Not supported method
				if (method.IsStatic || !method.IsPublic)
				{
					continue;
				}

				// SignalR requires all client methods should use Task as return type.
				if (!method.ReturnType.NameEqualsTo(typeof(Task)))
				{
					// TODO: Add invalid method handling
					continue;
				}

				classDeclaration = GenerateEvent(model, classDeclaration, method, out var delegateMethod);

				// binding statement
				bindingStatements.AddRange(GenerateCallbackMethodBindingStatement(model, method.Name, delegateMethod));
			}

			// Add binding method
			classDeclaration = classDeclaration.AddMembers(GenerateBindingMethod(model, bindingStatements));

			return classDeclaration;
		}

		private static MethodDeclarationSyntax GenerateBindingMethod(SemanticModel model, IEnumerable<StatementSyntax> bindingStatements)
		{
			return MethodDeclaration(model.GetVoidTypeSyntax(), "BindClientEvents")
				.WithModifiers(TokenList(ProtectedKeyword, OverrideKeyword))
				.WithBody(bindingStatements.AsBlock());
		}

		private static IEnumerable<StatementSyntax> GenerateCallbackMethodBindingStatement(SemanticModel model, string clientCallbackName, MethodDeclarationSyntax callbackMethod)
		{
			// HubConnection.On("Name", new Type[]{ ... }, (args, state) => Invoke((T1)arg[0], (T2)arg[1], ...), null);
			var bindExp = IdentifierName(nameof(HubProxy.HubConnection))
				.MemberAccess(nameof(HubConnection.On))
				.Invoke(StringLiteral(clientCallbackName), GenerateTypeArrayArgument(), GenerateMethodInvocation(),
					LiteralExpression(NullLiteralExpression));

			// ClientCallbackHandlers.Add(...)
			var addExp = IdentifierName("ClientCallbackHandlers")
				.MemberAccess(nameof(ICollection<IDisposable>.Add))
				.Invoke(bindExp);

			yield return addExp.AsStatement();

			// First argument: new Type[] {type1, type2 ...}
			ExpressionSyntax GenerateTypeArrayArgument()
			{
				return ArrayWithInitializer(
					model.GetTypeSyntax<Type>(),
					callbackMethod.ParameterList.Parameters.Select(i => (ExpressionSyntax)i.Type!.TypeOf()).ToArray());
			}


			// Second argument: (args, state) => Invoke((T1)arg[0], (T2)arg[1], ...)
			ExpressionSyntax GenerateMethodInvocation()
			{
				// no type required for lambda exp
				var parameterList = ParameterList(Parameter("args"), Parameter("state"));

				ExpressionSyntax ConvertParameterToInvokeArg(ParameterSyntax parameter, int index)
				{
					return IdentifierName("args").Element(Int32Literal(index)).Cast(parameter.Type!);
				}

				var body =
					IdentifierName(callbackMethod.Identifier)
						.Invoke(callbackMethod.ParameterList.Parameters.Select(ConvertParameterToInvokeArg).ToArray());

				return ParenthesizedLambdaExpression(parameterList, null, body);
			}
		}

		private static ClassDeclarationSyntax GenerateEvent(SemanticModel model, ClassDeclarationSyntax classDeclaration, MethodInfo hubClientMethod, out MethodDeclarationSyntax delegateMethod)
		{
			var eventType = GetDelegateTypeForMethod(model, hubClientMethod);
			var eventName = hubClientMethod.Name;

			var bindingStatements = new List<StatementSyntax>();

			var eventVariable = VariableDeclaration(eventType)
				.AddVariables(VariableDeclarator(eventName));

			var eventField = EventFieldDeclaration(eventVariable)
				.WithModifiers(TokenList(PublicKeyword))
				.AddAttributes(AttributeTargetSpecifier(Token(FieldKeyword)),
					Attribute(model.GetTypeNameSyntax<NonSerializedAttribute>()));


			// protected method to raise events
			var invokeMethod = MethodDeclaration(model.GetTypeSyntax<Task>(), $"On{eventName}")
				.WithModifiers(TokenList(ProtectedKeyword))
				.WithParameterList(GenerateParameterList())
				.WithBody(GenerateMethodBody());

			delegateMethod = invokeMethod;

			return classDeclaration.AddMembers(eventField, invokeMethod);

			BlockSyntax GenerateMethodBody()
			{
				// return event == null ? Task.CompletedTask : event(args)

				return ConditionalExpression(
					BinaryExpression(EqualsExpression, IdentifierName(eventName), LiteralExpression(NullLiteralExpression)),
					model.GetTypeSyntax<Task>().MemberAccess(nameof(Task.CompletedTask)),
					IdentifierName(eventName).Invoke(GenerateArgumentList())
				).Return().AsBlock();
			}

			ArgumentListSyntax GenerateArgumentList()
			{
				var items = hubClientMethod.GetParameters().Select(p => Argument(IdentifierName(p.Name)));
				return ArgumentList(SeparatedList(items));
			}

			ParameterListSyntax GenerateParameterList()
			{
				var items = new List<ParameterSyntax>();

				foreach (var p in hubClientMethod.GetParameters())
				{
					var parameterSyntax =
						Parameter(Identifier(p.Name))
							.WithType(model.GetTypeSyntax(p.ParameterType));

					items.Add(parameterSyntax);
				}

				return ParameterList(SeparatedList(items));
			}

		}

		/// <summary>
		/// Get a delegate type which are compatible with the specified method.
		/// </summary>
		/// <param name="model"></param>
		/// <param name="method">The method instance.</param>
		/// <returns>The corresponding delegate type which are compatible with <paramref name="method"/>. the delegate type will always be a variant of either the System.Action or the System.Func series.</returns>
		private static TypeSyntax GetDelegateTypeForMethod(SemanticModel model, MethodInfo method)
		{
			var paramList = method.GetParameters();

			if (method.ReturnType == typeof(void))
			{
				if (paramList.Length == 0)
				{
					return model.GetTypeSyntax<Action>();
				}

				var actionBaseType = model.Compilation.GetTypeByMetadataName($"System.Action`{paramList.Length}");

				if (actionBaseType == null)
				{
					throw new InvalidOperationException(
						$"The method \"{method.Name}\" has too many parameters and thus there's no proper delegate type can be used for client event declaration");
				}

				return actionBaseType.MakeGenericType(model, paramList.Select(i => model.GetEquivalentType(i.ParameterType)));
			}
			else
			{
				var funcBaseType = model.Compilation.GetTypeByMetadataName($"System.Func`{paramList.Length + 1}");

				if (funcBaseType == null)
				{
					throw new InvalidOperationException(
						$"The method \"{method.Name}\" has too many parameters and thus there's no proper delegate type can be used for client event declaration");
				}

				var typeArgList = new List<Type>();
				typeArgList.AddRange(paramList.Select(i => i.ParameterType));
				typeArgList.Add(method.ReturnType);

				return funcBaseType.MakeGenericType(model, typeArgList.Select(model.GetEquivalentType));
			}
		}
	}
}