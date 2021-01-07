using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;

namespace Sakura.AspNetCore.SignalR.HubProxies
{
	/// <summary>
	/// Base class for all generated strong typed hub proxy instance.
	/// </summary>
	public abstract class HubProxy : ISupportInitialize, IAsyncDisposable
	{
		/// <summary>
		/// The internal <see cref="Microsoft.AspNetCore.SignalR.Client.HubConnection"/> service instance.
		/// </summary>
		public HubConnection HubConnection { get; set; } = null!;

		/// <summary>
		/// The collection of all <see cref="IDisposable"/> created by the <see cref="HubConnection.On"/> method calls.
		/// </summary>
		protected ICollection<IDisposable> ClientCallbackHandlers { get; } = new Collection<IDisposable>();

		/// <summary>
		/// Initialize an new instance of <see cref="HubProxy"/> class.
		/// </summary>
		protected HubProxy()
		{
		}


		/// <summary>
		/// Starts a connection to the server.
		/// </summary>
		/// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
		/// </param>
		/// <returns>A <see cref="Task"/> that represents the asynchronous start.</returns>
		public virtual Task StartAsync(CancellationToken cancellationToken = default)
			=> HubConnection.StartAsync(cancellationToken);

		/// <summary>
		/// Stops a connection to the server.
		/// </summary>
		/// <param name="cancellationToken">The token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.
		/// </param>
		/// <returns>A <see cref="Task"/> that represents the asynchronous stop.</returns>
		public virtual Task StopAsync(CancellationToken cancellationToken = default)
			=> HubConnection.StopAsync(cancellationToken);

		/// <summary>
		/// Initialize core features like client message bindings for this proxy instance. This method will be executed automatically when <see cref="ISupportInitialize.EndInit" /> is called.
		/// </summary>
		protected virtual void Initialize()
		{
			// Ignore duplicated calls.
			if (IsInitialized)
			{
				return;
			}

			BindClientEvents();
		}

		/// <summary>
		/// Bind client message handlers.
		/// </summary>
		protected virtual void BindClientEvents()
		{
		}

		/// <summary>
		/// Get a value that indicates if the object has been initialized.
		/// </summary>
		public bool IsInitialized { get; private set; }

		/// <inheritdoc />
		public void BeginInit()
		{
			IsInitialized = false;
		}

		/// <inheritdoc />
		public void EndInit()
		{
			if (HubConnection == null)
			{
				throw new InvalidOperationException(
					$"The \"{nameof(HubConnection)}\" property must be specified during the initialization process.");
			}

			Initialize();
			IsInitialized = true;
		}

		/// <inheritdoc />
		public async ValueTask DisposeAsync()
		{
			// Dispose all callback handlers
			foreach (var item in ClientCallbackHandlers)
			{
				item?.Dispose();
			}

			// ReSharper disable once ConditionIsAlwaysTrueOrFalse
			if (HubConnection != null)
			{
				await HubConnection.DisposeAsync();
			}
		}
	}
}
