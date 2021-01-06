using System;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators
{
	public static class SemanticModelHelpers
	{
		public static INamedTypeSymbol GetEquivalentType(this SemanticModel model, Type type) =>
			model.Compilation.GetTypeByMetadataName(type.FullName ??
													throw new InvalidOperationException(
														"The target type does not have a valid full name."))
			?? throw new InvalidOperationException(
				$"Cannot get type \"{type.FullName}\"'s equivalent in the source code context.");

		public static INamedTypeSymbol GetEquivalentType<T>(this SemanticModel model) =>
			model.GetEquivalentType(typeof(T));

		public static TypeSyntax ToTypeSyntax(this ITypeSymbol symbol, SemanticModel model)
		{
			return SyntaxFactory.ParseTypeName(symbol.ToMinimalDisplayString(model, 0));
		}

		public static TypeSyntax GetTypeSyntax(this SemanticModel model, Type type) =>
			(model.GetEquivalentType(type) ??
			 throw new InvalidOperationException(
				 $"Cannot get type \"{type.FullName}\"'s equivalent in the source code context.")).ToTypeSyntax(model);

		public static TypeSyntax GetTypeSyntax<T>(this SemanticModel model) =>
			model.GetEquivalentType<T>().ToTypeSyntax(model);

		public static NameSyntax GetTypeNameSyntax(this SemanticModel model, Type type) =>
			SyntaxFactory.ParseName(model.GetEquivalentType(type).ToMinimalDisplayString(model, 0));

		public static NameSyntax GetTypeNameSyntax<T>(this SemanticModel model)
			=> SyntaxFactory.ParseName(model.GetEquivalentType<T>().ToMinimalDisplayString(model, 0));

		public static TypeSyntax GetVoidTypeSyntax(this SemanticModel model)
			=> model.Compilation.GetSpecialType(SpecialType.System_Void).ToTypeSyntax(model);

		public static TypeSyntax GetObjectTypeSyntax(this SemanticModel model)
			=> model.Compilation.ObjectType.ToTypeSyntax(model);
	}
}