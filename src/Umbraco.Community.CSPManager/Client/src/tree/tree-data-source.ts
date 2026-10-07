import { CspConstants } from '@/constants';
import type { UmbTreeItemModel, UmbTreeRootItemsRequestArgs, UmbTreeChildrenOfRequestArgs, UmbTreeAncestorsOfRequestArgs } from '@umbraco-cms/backoffice/tree';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbLocalizationController } from '@umbraco-cms/backoffice/localization-api';
import { UmbCspDefinitionRepository } from '@/repository/csp-definition.repository.js';
import type { CspApiDomainPolicy } from '@/api';

export interface CspTreeItemModel extends UmbTreeItemModel {
	icon: string;
}

const frontendParent = {
	unique: CspConstants.policyTypes.frontend.value,
	entityType: CspConstants.workspace.entityType,
};

export class CspTreeDataSource {
	#repository: UmbCspDefinitionRepository;
	#localize: UmbLocalizationController;

	constructor(host: UmbControllerHost) {
		this.#repository = new UmbCspDefinitionRepository(host);
		this.#localize = new UmbLocalizationController(host);
	}

	async getRootItems(_args: UmbTreeRootItemsRequestArgs) {
		// Domain policies hang under Frontend; only show the expand arrow when there are any.
		const { data: domainPolicies } = await this.#repository.getDomainPolicies();

		const items: CspTreeItemModel[] = [
			{
				unique: CspConstants.policyTypes.backoffice.value,
				entityType: CspConstants.workspace.entityType,
				name: CspConstants.policyTypes.backoffice.label,
				hasChildren: false,
				icon: CspConstants.policyTypes.backoffice.icon,
				isFolder: false,
				parent: {
					unique: null,
					entityType: CspConstants.entityTypes.cspPolicyRoot,
				},
			},
			{
				unique: CspConstants.policyTypes.frontend.value,
				entityType: CspConstants.workspace.entityType,
				name: CspConstants.policyTypes.frontend.label,
				hasChildren: (domainPolicies?.length ?? 0) > 0,
				icon: CspConstants.policyTypes.frontend.icon,
				isFolder: false,
				parent: {
					unique: null,
					entityType: CspConstants.entityTypes.cspPolicyRoot,
				},
			},
		];

		return {
			data: {
				items,
				total: items.length,
				totalBefore: 0,
				totalAfter: 0,
			},
		};
	}

	async getChildrenOf(args: UmbTreeChildrenOfRequestArgs) {
		// Only the Frontend node has children: the domain policies that override it.
		if (args.parent.unique !== CspConstants.policyTypes.frontend.value) {
			return { data: { items: [], total: 0, totalBefore: 0, totalAfter: 0 } };
		}

		const { data: policies, error } = await this.#repository.getDomainPolicies();
		if (error || !policies) {
			return { error };
		}

		const items: CspTreeItemModel[] = policies.map((policy) => this.#toTreeItem(policy));

		return {
			data: {
				items,
				total: items.length,
				totalBefore: 0,
				totalAfter: 0,
			},
		};
	}

	async getAncestorsOf(args: UmbTreeAncestorsOfRequestArgs) {
		// A domain policy sits under Frontend; the global policies are at the root.
		if (!args.treeItem.unique || args.treeItem.unique === CspConstants.policyTypes.frontend.value || args.treeItem.unique === CspConstants.policyTypes.backoffice.value) {
			return { data: [] };
		}

		return {
			data: [
				{
					unique: CspConstants.policyTypes.frontend.value,
					entityType: CspConstants.workspace.entityType,
					name: CspConstants.policyTypes.frontend.label,
					hasChildren: true,
					icon: CspConstants.policyTypes.frontend.icon,
					isFolder: false,
					parent: { unique: null, entityType: CspConstants.entityTypes.cspPolicyRoot },
				} satisfies CspTreeItemModel,
			],
		};
	}

	#toTreeItem(policy: CspApiDomainPolicy): CspTreeItemModel {
		// The name is rendered by the tree item as text; never as HTML.
		const label = policy.isOrphaned || !policy.domainName
			? this.#localize.term('cspManagerDomainPolicy_removedDomain', policy.domainKey.slice(0, 8))
			: policy.domainName;
		// An inactive policy is kept but the domain uses the Frontend policy (or no header).
		const name = policy.enabled ? label : this.#localize.term('cspManagerDomainPolicy_treeInactive', label);

		return {
			unique: policy.id,
			entityType: CspConstants.workspace.entityType,
			name,
			hasChildren: false,
			icon: policy.isOrphaned ? CspConstants.icons.orphanedDomainPolicy : CspConstants.icons.domainPolicy,
			isFolder: false,
			parent: frontendParent,
		};
	}
}
