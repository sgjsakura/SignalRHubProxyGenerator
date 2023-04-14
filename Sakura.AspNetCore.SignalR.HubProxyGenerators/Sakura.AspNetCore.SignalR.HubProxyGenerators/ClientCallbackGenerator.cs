using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators;

/// <summary>
///     Provide methods to generate SignalR client callback message handlers.
/// </summary>
public abstract class ClientCallbackGenerator
{
	/// <summary>
	///     Generate client callback methods for a hub proxy type.
	/// </summary>
	/// <param name="model">The <see cref="SemanticModel" /> instance used to provide semantic data.</param>
	/// <param name="classDeclaration">
	///     The <see cref="ClassDeclarationSyntax" /> instance represents as the new created hub
	///     proxy class.
	/// </param>
	/// <param name="hubClientType">The <see cref="Type" /> definition of the hub client interface.</param>
	/// <returns>
	///     The generated new <see cref="ClassDeclarationSyntax" /> for the hub proxy used to replace the original
	///     <paramref name="classDeclaration" /> instance.
	/// </returns>
	public abstract ClassDeclarationSyntax Generate(SemanticModel model, ClassDeclarationSyntax classDeclaration,
		Type hubClientType);
}