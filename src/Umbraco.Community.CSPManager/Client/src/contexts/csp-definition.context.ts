import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UmbContextBase } from '@umbraco-cms/backoffice/class-api';
import { UmbContextToken } from '@umbraco-cms/backoffice/context-api';
import type { CspApiDefinition } from '@/api';
import { UmbCspDefinitionRepository } from '../repository/csp-definition.repository.js';

export const UMB_CSP_DEFINITION_CONTEXT = new UmbContextToken<UmbCspDefinitionContext>(
	'UmbCspDefinitionContext',
	'csp-manager.definition'
);

export class UmbCspDefinitionContext extends UmbContextBase {
	#repository: UmbCspDefinitionRepository;

	constructor(host: UmbControllerHost) {
		super(host, UMB_CSP_DEFINITION_CONTEXT);
		this.#repository = new UmbCspDefinitionRepository(host);
	}

	async load(isBackOffice: boolean) {
		return await this.#repository.get(isBackOffice);
	}

	async loadById(id: string) {
		return await this.#repository.getById(id);
	}

	async save(definition: CspApiDefinition) {
		return await this.#repository.save(definition);
	}

	async getDomainPolicies() {
		return await this.#repository.getDomainPolicies();
	}

	async getDomains() {
		return await this.#repository.getDomains();
	}

	async deleteDomainPolicy(id: string) {
		return await this.#repository.delete(id);
	}

	async moveDomainPolicy(id: string, domainKey: string) {
		return await this.#repository.move(id, domainKey);
	}
}

export default UmbCspDefinitionContext;
