using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Sakura.AspNetCore.SignalR.HubProxies;

[assembly: NetCoreRuntimeLocation("test", IgnoredFiles = new[] { "abc", "def" })]


namespace Sakura.AspNetCore.SignalR.HubProxies
{
	/// <summary>
	/// Control the hub proxy generation options for an entire assembly or specified hub class.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false)]
	public class HubProxyGenerationAttribute : Attribute
	{
		/// <summary>
		/// The name of the namespace used to put all generated hub proxy types.
		/// </summary>
		public string? RootNamespace { get; set; }

		/// <summary>
		/// If true, the generated hub proxy class will not contain any hub client message events.
		/// </summary>
		public HubClientCallbackGenerateMode ClientCallbackGenerationMode { get; set; } =
			HubClientCallbackGenerateMode.AsyncEvent;

		/// <summary>
		/// The client type of the hub. If this property is not <c>null</c>, it will be used rather than the inferred hub client type.
		/// </summary>
		public Type? HubClientType { get; set; }

		/// <summary>
		/// The path of the assembly which contains the hub server class.
		/// </summary>
		public string? HubAssemblyPath { get; set; }


		/// <summary>
		/// The format string used to generate the proxy type name of a hub. Allowed placeholders:
		/// <list type="circle">
		///	<item>
		/// <term>{0}</term>
		/// <description>The name of the hub type.</description>
		/// </item>
		/// </list>
		/// </summary>
		public string? TypeNameFormat { get; set; } = DefaultTypeNameFormat;

		/// <summary>
		/// The default value of <see cref="TypeNameFormat"/>. This field is constant.
		/// </summary>
		public const string DefaultTypeNameFormat = "{0}Proxy";
	}
}