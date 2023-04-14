using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators;

/// <summary>
///     Provide extension methods for <see cref="SemanticModel" />. This class is static.
/// </summary>
internal static class SemanticModelHelpers
{
	public static INamedTypeSymbol GetEquivalentType(this SemanticModel model, Type type)
	{
		return model.Compilation.GetTypeByMetadataName(type.FullName ??
		                                               throw new InvalidOperationException(
			                                               "The target type does not have a valid full name."))
		       ?? throw new InvalidOperationException(
			       $"Cannot get type \"{type.FullName}\"'s equivalent in the source code context.");
	}

	public static INamedTypeSymbol GetEquivalentType<T>(this SemanticModel model)
	{
		return model.GetEquivalentType(typeof(T));
	}

	public static TypeSyntax ToTypeSyntax(this ITypeSymbol symbol, SemanticModel model)
	{
		return SyntaxFactory.ParseTypeName(symbol.ToMinimalDisplayString(model, 0));
	}

	public static TypeSyntax GetTypeSyntax(this SemanticModel model, Type type)
	{
		return (model.GetEquivalentType(type) ??
		        throw new InvalidOperationException(
			        $"Cannot get type \"{type.FullName}\"'s equivalent in the source code context."))
			.ToTypeSyntax(model);
	}

	public static TypeSyntax GetTypeSyntax<T>(this SemanticModel model)
	{
		return model.GetEquivalentType<T>().ToTypeSyntax(model);
	}

	public static NameSyntax GetTypeNameSyntax(this SemanticModel model, Type type)
	{
		return SyntaxFactory.ParseName(model.GetEquivalentType(type).ToMinimalDisplayString(model, 0));
	}

	public static NameSyntax GetTypeNameSyntax<T>(this SemanticModel model)
	{
		return SyntaxFactory.ParseName(model.GetEquivalentType<T>().ToMinimalDisplayString(model, 0));
	}

	public static TypeSyntax GetVoidTypeSyntax(this SemanticModel model)
	{
		return model.Compilation.GetSpecialType(SpecialType.System_Void).ToTypeSyntax(model);
	}

	public static TypeSyntax GetObjectTypeSyntax(this SemanticModel model)
	{
		return model.Compilation.ObjectType.ToTypeSyntax(model);
	}
}