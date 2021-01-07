using System;

namespace Sakura.AspNetCore.SignalR.HubProxies
{
	/// <summary>
	/// Specify the location for .NET Core as well as .NET 5 runtime libraries.
	/// </summary>
	[AttributeUsage(AttributeTargets.Assembly)]
	public class NetCoreRuntimeLocationAttribute : Attribute
	{
		/// <summary>
		/// The name of the CLR core library. This library will be ignored by default if <see cref="IgnoredFiles"/> is not specified.
		/// </summary>
		public static string CoreLibraryName { get; } = typeof(object).Assembly.GetName().Name;

		/// <summary>
		/// The default value for <see cref="SearchPattern"/>. This field is constant.
		/// </summary>
		public const string DefaultSearchPattern = "*.dll";

		/// <summary>
		/// The directories of .NET core runtime libraries, environment variables will be expanded with <see cref="Environment.ExpandEnvironmentVariables"/>. Sub directories will also be automatically included for each location.
		/// </summary>
		public string[] Directories { get; }

		/// <summary>
		/// The file names which should be ignored while loading runtime libraries. You may add files in this list in necessary to avoid probable SXS problems.
		/// </summary>
		public string[]? IgnoredFiles { get; set; }

		/// <summary>
		/// The search pattern used to get file list. If this property is not set, the 
		/// </summary>
		public string? SearchPattern { get; set; } = DefaultSearchPattern;

		/// <summary>
		/// Initialize a new instance of <see cref="NetCoreRuntimeLocationAttribute"/>.
		/// </summary>
		/// <param name="directories">The directories of .NET core runtime libraries, environment variables will be expanded with <see cref="Environment.ExpandEnvironmentVariables"/>. Sub directories will also be automatically included for each location.</param>
		public NetCoreRuntimeLocationAttribute(params string[] directories)
		{
			Directories = directories;
		}
	}
}