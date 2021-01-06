using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators
{
	/// <summary>
	/// Provide methods to generate SignalR client callback message handlers.
	/// </summary>
	public abstract class ClientCallbackGenerator
	{
		public abstract ClassDeclarationSyntax Generate(SemanticModel model, ClassDeclarationSyntax classDeclaration, Type hubClientType);
	}
}
