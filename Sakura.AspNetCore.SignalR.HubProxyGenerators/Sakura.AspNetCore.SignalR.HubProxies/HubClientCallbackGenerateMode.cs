namespace Sakura.AspNetCore.SignalR.HubProxies
{
	/// <summary>
	///     The generation mode for hub client messages.
	/// </summary>
	public enum HubClientCallbackGenerateMode
	{
		/// <summary>
		///     The client message will be encapsulated as events. Task-based asynchronicity it NOT supported in this mode.
		/// </summary>
		AsyncEvent,

		/// <summary>
		///     The client message will be encapsulated as partial methods and developers can provide implementation for them.
		///     Task-based asynchronicity it NOT supported in this mode.
		/// </summary>
		PartialMethod
	}
}