import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import { CspConstants } from '@/constants';

export interface AddDomainPolicyModalData {
	/** Localization key of the headline; defaults to the "Add Domain Policy" headline. */
	headlineKey?: string;
}

export interface AddDomainPolicyModalValue {
	domainKey: string;
}

export const ADD_DOMAIN_POLICY_MODAL = new UmbModalToken<AddDomainPolicyModalData, AddDomainPolicyModalValue>(
	CspConstants.domainPolicy.addModalAlias,
	{
		modal: {
			type: 'sidebar',
			size: 'small',
		},
	},
);
