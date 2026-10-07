export default {
    "uSync": {
			"CspDefinition": "CSP"
    },
		"usyncpublish": {
			"CspDefinition": "CSP"
	},
	cspManagerDomainPolicy: {
		addAction: 'Add Domain Policy',
		addModalHeadline: 'Add Domain Policy',
		addModalEmpty:
			'No domains available. Either every domain already has a CSP policy, or no domains are configured under Culture and Hostnames.',
		addModalLoadFailed: 'The domains could not be loaded.',
		unknownCulture: 'No culture',
		unknownContent: 'Unknown content',
		removedDomain: 'Removed domain (%0%…)',
		workspaceHeadline: '%0% CSP Management',
		orphanedPolicyInfo:
			'The domain this policy was created for no longer exists, so it applies to no requests. Delete it, or re-add the domain to bring it back into use.',
		openContent: 'Open content item',
		statusLabel: 'Domain policy',
		statusActive: 'Active',
		statusInactiveFallback: 'Inactive: uses the Frontend policy',
		statusInactiveNoHeader: 'Inactive: no CSP header',
		summaryActive: '%0% uses this policy.',
		summaryActiveReportOnly: '%0% uses this policy, in report-only mode.',
		summaryFallback: '%0% uses the Frontend policy.',
		summaryNoHeader: '%0% gets no CSP header.',
		treeInactive: '%0% (inactive)',
		deleteAction: 'Delete domain policy',
		deleting: 'Deleting...',
		deleteConfirmHeadline: 'Delete domain policy',
		deleteConfirmContent: 'Delete the CSP policy for %0%? Requests on this domain will use the Frontend policy. This cannot be undone.',
		deletedHeadline: 'Policy deleted',
		deletedMessage: 'The CSP policy for %0% has been deleted.',
		deleteFailedHeadline: 'Delete failed',
		deleteFailedMessage: 'An error occurred while deleting the CSP policy.',
	},
};
