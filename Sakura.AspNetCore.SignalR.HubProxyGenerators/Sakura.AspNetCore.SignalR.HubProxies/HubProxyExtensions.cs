using System;
using Microsoft.AspNetCore.SignalR.Client;

namespace Sakura.AspNetCore.SignalR.HubProxies
{
	/// <summary>
	///     Provide extension methods for creating hub proxy services. This class is static.
	/// </summary>
	public static class HubProxyExtensions
	{
		/// <summary>
		///     Create a new hub proxy service instance using the specified <see cref="HubConnection" /> object.
		/// </summary>
		/// <typeparam name="TProxy">The type of the hub proxy.</typeparam>
		/// <param name="hubConnection">The internal <see cref="HubConnection" /> service object.</param>
		/// <returns>The created hub proxy instance.</returns>
		/// <exception cref="ArgumentNullException">The <paramref name="hubConnection" /> is <c>null</c>.</exception>
		public static TProxy CreateProxy<TProxy>(this HubConnection hubConnection)
			where TProxy : HubProxy, new()
		{
			// argument check
			if (hubConnection == null) throw new ArgumentNullException(nameof(hubConnection));

			var result = new TProxy();

			// enter initialization phase and set the hub connection service.
			result.BeginInit();
			result.HubConnection = hubConnection;
			result.EndInit();

			return result;
		}
	}
}