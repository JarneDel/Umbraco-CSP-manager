using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;

namespace Umbraco.Community.CSPManager.Notifications.Handlers;

/// <summary>
/// Invalidates the cached definition when one is saved or deleted, on this server and - through
/// <see cref="CspDistributedCacheRefresher"/> - on every other server.
/// </summary>
internal sealed class CspSavedNotificationHandler
	: INotificationHandler<CspSavedNotification>, INotificationHandler<CspDeletedNotification>
{
	private readonly IAppPolicyCache _runtimeCache;
	private readonly DistributedCache _distributedCache;

	public CspSavedNotificationHandler(
		AppCaches appCaches,
		DistributedCache distributedCache
	)
	{
		_runtimeCache = appCaches.RuntimeCache;
		_distributedCache = distributedCache;
	}

	public void Handle(CspSavedNotification notification) => Invalidate(notification);

	// The refresher's payload type is CspSavedNotification; it only identifies which cache entry
	// to clear, so the same payload serves deletes.
	public void Handle(CspDeletedNotification notification) => Invalidate(new CspSavedNotification(notification.CspDefinition));

	private void Invalidate(CspSavedNotification payload)
	{
		string cacheKey = CspCacheKeys.For(payload.CspDefinition);

		// Clear locally first so this server serves the new policy immediately, then broadcast to the rest.
		_runtimeCache.ClearByKey(cacheKey);
		_distributedCache.RefreshByPayload(CspDistributedCacheRefresher.UniqueId, [payload]);
	}
}