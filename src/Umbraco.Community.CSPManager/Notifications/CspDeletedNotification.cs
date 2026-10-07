using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.CSPManager.Models;

namespace Umbraco.Community.CSPManager.Notifications;

/// <summary>
/// Notification published after a <see cref="Models.CspDefinition"/> (a domain policy; the global
/// policies can't be deleted) has been deleted and the deletion committed.
/// </summary>
public class CspDeletedNotification : INotification
{
	public CspDeletedNotification(CspDefinition cspDefinition)
	{
		CspDefinition = cspDefinition;
	}

	/// <summary>
	/// Gets or sets the definition as it was before it was deleted.
	/// </summary>
	public CspDefinition CspDefinition { get; set; }
}
