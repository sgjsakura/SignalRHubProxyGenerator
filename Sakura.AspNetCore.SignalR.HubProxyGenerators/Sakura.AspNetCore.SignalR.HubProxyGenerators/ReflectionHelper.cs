using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators
{
	/// <summary>
	/// Provide extension method for cross appdomain reflection operations. This class is static.
	/// </summary>
	public static class ReflectionHelper
	{
		/// <summary>
		/// Indicates whether 2 types can be considered as equivalent using full name comparision.
		/// </summary>
		/// <param name="type1">The first type instance.</param>
		/// <param name="type2">The second type instance.</param>
		/// <returns>If <paramref name="type1"/> equals to <paramref name="type2"/>, returns <c>true</c>; otherwise, returns <c>false</c>.</returns>
		/// <remarks>
		/// In order to suppress the type mapping difference caused by different assembly version or app domains, this method uses <see cref="Type.FullName"/> to compare 2 types.
		/// </remarks>
		public static bool NameEqualsTo(this Type type1, Type type2)
		{
			return type1.FullName == type2.FullName;
		}
	}
}
