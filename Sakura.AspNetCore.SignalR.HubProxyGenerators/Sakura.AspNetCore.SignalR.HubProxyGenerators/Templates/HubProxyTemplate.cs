using Microsoft.AspNetCore.SignalR.Client;

namespace Sakura.AspNetCore.SignalR.HubProxyGenerators
{
	class HubProxyTemplate
	{
		public HubConnection HubConnection { get; }

		public HubProxyTemplate(HubConnection hubConnection)
		{
			HubConnection = hubConnection;
		}
	}
}