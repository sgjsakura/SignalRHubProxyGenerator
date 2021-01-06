namespace Sakura.AspNetCore.SignalR.HubProxies
{
	/// <summary>
	/// The generation mode for hub client messages.
	/// </summary>
	public enum HubClientCallbackGenerateMode
	{
		/// <summary>
		/// No client message will be generated.
		/// </summary>
		None,
		/// <summary>
		/// The client message will be encapsulated as events. Task-based asynchronicity it NOT supported in this mode.
		/// </summary>
		Event,
		/// <summary>
		/// The client message will be encapsulated as partial methods and developers can provide implementation for them. Task-based asynchronicity it NOT supported in this mode.
		/// </summary>
		PartialMethod,
		/// <summary>
		/// The client message will be encapsulated as synchronous delegates.
		/// </summary>
		Delegate,
		/// <summary>
		/// The client message will be encapsulated as asynchronous delegates.
		/// </summary>
		AsyncDelegate
	}
}