using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators
{
	/// <summary>
	/// Provide helper method for syntax generation. This class is static.
	/// </summary>
	public static class SyntaxHelper
	{

		/// <summary>
		/// Get all data of a specified attribute type from a symbol definition.
		/// </summary>
		/// <typeparam name="T">The type of the attribute.</typeparam>
		/// <param name="symbol">The symbol object.</param>
		/// <returns>All the <see cref="AttributeData"/> instance with type <typeparamref name="T"/> defined on the <paramref name="symbol"/>. If no matching attribute is found, this method will returns a empty collection.</returns>
		public static IEnumerable<AttributeData> GetAttributes<T>(this ISymbol symbol)
			where T : Attribute
			=> symbol.GetAttributes().Where(i =>
				i.AttributeClass != null && SymbolDisplay.ToDisplayString(i.AttributeClass) == typeof(T).FullName);

		/// <summary>
		/// Get all data of a specified attribute type from a symbol definition.
		/// </summary>
		/// <typeparam name="T">The type of the attribute.</typeparam>
		/// <param name="symbol">The symbol object.</param>
		/// <returns>The <see cref="AttributeData"/> instance with type <typeparamref name="T"/> defined on the <paramref name="symbol"/>. If no matching attribute is found, this method will returns <c>null</c>; If more than one instance is defined, <see cref="InvalidOperationException"/> will be raised.</returns>
		/// <exception cref="InvalidOperationException">There is more than one attribute with the same type defined on the <paramref name="symbol"/>.</exception>
		public static AttributeData? GetAttribute<T>(this ISymbol symbol)
			where T : Attribute
			=> symbol.GetAttributes<T>().SingleOrDefault();

		public static T GetValue<T>(this AttributeData attributeData, string key)
		{
			var item = attributeData.NamedArguments.SingleOrDefault(i => i.Key == key);

			// Not found
			if (item.Key == null)
			{
				return default!;
			}

			return (T)item.Value.Value!;
		}

		public static StatementSyntax AsStatement(this ExpressionSyntax expression) =>
			ExpressionStatement(expression);

		public static BlockSyntax AsBlock(this StatementSyntax statement) => Block(statement);
		public static BlockSyntax AsBlock(this IEnumerable<StatementSyntax> statements) => Block(statements);

		public static ParameterListSyntax ParameterList(params ParameterSyntax[] parameters)
			=> SyntaxFactory.ParameterList(SeparatedList(parameters));

		public static SeparatedSyntaxList<TNode> AsSeparatedList<TNode>(this TNode item)
			where TNode : SyntaxNode
			=> SyntaxFactory.SeparatedList(new[] { item });

		public static ParameterSyntax Parameter(string identifier, TypeSyntax? type = default)
			=> SyntaxFactory.Parameter(Identifier(identifier)).WithType(type);

		public static ArgumentSyntax AsArgument(this ExpressionSyntax expression) => Argument(expression);

		public static ElementAccessExpressionSyntax Element(this ExpressionSyntax expression,
			params ExpressionSyntax[] indexes) => ElementAccessExpression(expression,
			BracketedArgumentList(SyntaxFactory.SeparatedList(indexes.Select(AsArgument))));

		/// <summary>
		/// Access a chained member of an expression. 
		/// </summary>
		/// <param name="parent">The parent expression instance.</param>
		/// <param name="memberName">The name of the member to be accessed for the <paramref name="parent"/>.</param>
		/// <param name="memberPaths">The names of all following-up members to be accessed.</param>
		/// <returns>The generated <see cref="MemberAccessExpressionSyntax"/> instance.</returns>
		public static MemberAccessExpressionSyntax MemberAccess(this ExpressionSyntax parent, string memberName, params string[] memberPaths)
		{
			// First member
			var result = MemberAccessExpression(SimpleMemberAccessExpression, parent, IdentifierName(memberName));

			// rest members
			return memberPaths.Aggregate(result,
				(current, name) => MemberAccessExpression(SimpleMemberAccessExpression,
					current, IdentifierName(name)));
		}

		public static LiteralExpressionSyntax StringLiteral(string value)
			=> LiteralExpression(StringLiteralExpression, Literal(value));

		public static LiteralExpressionSyntax Int32Literal(int value)
			=> LiteralExpression(NumericLiteralExpression, Literal(value));

		/// <summary>
		/// Conditional access a chained member of an expression. 
		/// </summary>
		/// <param name="parent">The parent expression instance.</param>
		/// <param name="memberName">The name of the member to be accessed for the <paramref name="parent"/>.</param>
		/// <param name="memberPaths">The names of all following-up members to be accessed.</param>
		/// <returns>The generated <see cref="ConditionalAccessExpressionSyntax"/> instance.</returns>
		public static ConditionalAccessExpressionSyntax ConditionalMemberAccess(this ExpressionSyntax parent, string memberName, params string[] memberPaths)
		{
			var result = ConditionalAccessExpression(parent, IdentifierName(memberName));

			return memberPaths.Aggregate(result,
				(current, name) => ConditionalAccessExpression(current, IdentifierName(name)));
		}

		public static TypeOfExpressionSyntax TypeOf(this TypeSyntax type) => TypeOfExpression(type);

		public static CastExpressionSyntax Cast(this ExpressionSyntax expression, TypeSyntax type) =>
			CastExpression(type, expression);

		public static AwaitExpressionSyntax Await(this ExpressionSyntax expression) => AwaitExpression(expression);

		public static IdentifierNameSyntax ValueKeyword => IdentifierName("value");

		public static AttributeListSyntax ToList(this AttributeSyntax attribute, AttributeTargetSpecifierSyntax? target = default)
			=> AttributeList(target, SeparatedList(attribute));

		public static MemberDeclarationSyntax AddAttributes(this MemberDeclarationSyntax member, AttributeTargetSpecifierSyntax? target,
			params AttributeSyntax[] attributes)
			=> member.AddAttributeLists(attributes.Select(i => i.ToList(target)).ToArray());

		public static ArrayCreationExpressionSyntax ArrayWithInitializer(TypeSyntax elementType,
			params ExpressionSyntax[] initializingValues)
		{
			return ArrayCreationExpression(elementType.ToArrayType(Enumerable.Empty<ExpressionSyntax>()), // Type[]
				InitializerExpression(ArrayInitializerExpression, SeparatedList(initializingValues)));
		}

		public static InvocationExpressionSyntax Invoke(this ExpressionSyntax target, ArgumentListSyntax arguments)
			=> InvocationExpression(target, arguments);

		public static InvocationExpressionSyntax Invoke(this ExpressionSyntax target, params ExpressionSyntax[] arguments)
			=> InvocationExpression(target, ArgumentList(arguments));

		public static ReturnStatementSyntax Return(this ExpressionSyntax expression) =>
			ReturnStatement(expression);

		public static ArrayTypeSyntax ToArrayType(this TypeSyntax type) => ArrayType(type);

		public static ArrayTypeSyntax ToArrayType(this TypeSyntax type, params ArrayRankSpecifierSyntax[] arrayRanks) =>
			ArrayType(type, SyntaxFactory.List(arrayRanks));

		public static ArrayTypeSyntax
			ToArrayType(this TypeSyntax type, params IEnumerable<ExpressionSyntax>[] arrayRanks) =>
			ArrayType(type,
				SyntaxFactory.List(arrayRanks.Select(i =>
					ArrayRankSpecifier(SyntaxFactory.SeparatedList(i)))));

		public static SeparatedSyntaxList<T> SeparatedList<T>(params T[] args)
			where T : SyntaxNode
			=> SyntaxFactory.SeparatedList(args);

		public static SyntaxList<T> List<T>(params T[] args)
			where T : SyntaxNode
			=> SyntaxFactory.List(args);

		public static SyntaxTokenList TokenList(params SyntaxKind[] syntaxKinds)
		{
			var items = syntaxKinds.Select(Token);
			return SyntaxFactory.TokenList(items);
		}

		/// <summary>
		/// Generate the <see cref="ArgumentListSyntax"/> instance from a list of argument expressions.
		/// </summary>
		/// <param name="expressions">The array of <see cref="ExpressionSyntax"/> which represents to each argument.</param>
		/// <returns>The generated <see cref="ArgumentSyntax"/> instance.</returns>
		public static ArgumentListSyntax ArgumentList(params ExpressionSyntax[] expressions)
		{
			return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(expressions.Select(Argument)));
		}

		public static BaseListSyntax BaseList(params BaseTypeSyntax[] items)
		{
			return SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(items));
		}

		public static AccessorListSyntax AccessorList(params AccessorDeclarationSyntax[] accessors)
		{
			return SyntaxFactory.AccessorList(List(accessors));
		}


		/// <summary>
		/// Generate a Task type with the specified task result value type.
		/// </summary>
		/// <param name="model">The <see cref="SemanticModel"/> instance.</param>
		/// <param name="innerType">The task result type, can be <c>null</c> if the task has no result.</param>
		/// <returns>The generated <see cref="System.Threading.Tasks.Task"/> or <see cref="System.Threading.Tasks.Task{TResult}"/> type.</returns>
		public static TypeSyntax MakeTaskType(SemanticModel model, ITypeSymbol? innerType)
		{
			if (innerType == null)
			{
				return model.GetTypeSyntax<Task>();
			}

			var baseType = model.GetEquivalentType(typeof(Task<>));
			return baseType.MakeGenericType(model, innerType);
		}

		public static GenericNameSyntax MakeGenericType(this ITypeSymbol genericTypeDefinition, SemanticModel model,
			params ITypeSymbol[] typeArguments) =>
			genericTypeDefinition.MakeGenericType(model, (IEnumerable<ITypeSymbol>)typeArguments);

		/// <summary>
		/// Generate a <see cref="GenericNameSyntax"/> used to present as a closed generic type.
		/// </summary>
		/// <param name="genericTypeDefinition">The type definition symbol of the generic type.</param>
		/// <param name="model">The <see cref="SemanticModel"/> instance.</param>
		/// <param name="typeArguments">Type symbols for all type arguments.</param>
		/// <returns>The generated <see cref="GenericNameSyntax"/> instance.</returns>
		public static GenericNameSyntax MakeGenericType(this ITypeSymbol genericTypeDefinition, SemanticModel model,
			IEnumerable<ITypeSymbol> typeArguments)
		{
			// Type arg list
			var typeArgs = TypeArgumentList();
			typeArgs = typeArgs.AddArguments(typeArguments.Select(i => i.ToTypeSyntax(model)).ToArray());

			// Omit generic args for open generic type definition
			var format = new SymbolDisplayFormat(genericsOptions: SymbolDisplayGenericsOptions.None);

			return GenericName(Identifier(genericTypeDefinition.ToMinimalDisplayString(model, 0, format)))
				.WithTypeArgumentList(typeArgs);
		}

		/// <summary>
		/// try to determine if a type represents as a task and get its result type.
		/// </summary>
		/// <param name="type">The <see cref="Type"/> to be determining.</param>
		/// <param name="resultType">If the <paramref name="type"/> is a task with result, returns the result type; otherwise, returns <c>null</c>.</param>
		/// <returns>If the <paramref name="type"/> is a task (with or without an result), returns <c>true</c>; otherwise, returns <c>false</c>.</returns>
		public static bool TryGetTaskResultType(Type type, out Type? resultType)
		{
			var taskFullName = typeof(Task).FullName;
			var taskWithResultFullName = typeof(Task<>).FullName;

			resultType = null;

			if (type.FullName == taskFullName)
			{
				return true;
			}

			if (type.IsGenericType)
			{
				var defType = type.GetGenericTypeDefinition();

				if (defType.FullName == taskWithResultFullName)
				{
					resultType = type.GenericTypeArguments[0];
					return true;
				}
			}

			return false;
		}
	}
}