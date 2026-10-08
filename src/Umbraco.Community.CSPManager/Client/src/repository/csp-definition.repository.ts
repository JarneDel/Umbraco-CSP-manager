import { UmbRepositoryBase } from "@umbraco-cms/backoffice/repository";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { Definitions, Domains, type CspApiDefinition } from '../api';

export class UmbCspDefinitionRepository extends UmbRepositoryBase {
	constructor(host: UmbControllerHost) {
		super(host);
	}

	/**
	 * Get CSP definition by type (front-end or back-office)
	 */
	async get(isBackOffice: boolean) {
		const { data, error } = await tryExecute(
			this,
			Definitions.getUmbracoCspApiV1Definitions({
				query: { isBackOffice },
			}),
			{ disableNotifications: false }
		);

		if (data) {
			return { data };
		}

		return { error };
	}

	/**
	 * Get a CSP definition (global or domain policy) by its id
	 */
	async getById(id: string) {
		const { data, error } = await tryExecute(
			this,
			Definitions.getUmbracoCspApiV1DefinitionsById({
				path: { id },
			}),
			{ disableNotifications: false }
		);

		if (data) {
			return { data };
		}

		return { error };
	}

	/**
	 * Get every domain policy (including orphaned ones whose domain was removed)
	 */
	async getDomainPolicies() {
		const { data, error } = await tryExecute(this, Definitions.getUmbracoCspApiV1DefinitionsDomainPolicies(), {
			disableNotifications: false,
		});

		if (data) {
			return { data };
		}

		return { error };
	}

	/**
	 * Get the Umbraco domains a domain policy can be created for
	 */
	async getDomains() {
		const { data, error } = await tryExecute(this, Domains.getUmbracoCspApiV1Domains(), { disableNotifications: false });

		if (data) {
			return { data };
		}

		return { error };
	}

	/**
	 * Delete a domain policy
	 */
	async delete(id: string) {
		const { error } = await tryExecute(
			this,
			Definitions.deleteUmbracoCspApiV1DefinitionsById({
				path: { id },
			}),
			{ disableNotifications: false }
		);

		return { error };
	}

	/**
	 * Move an orphaned domain policy to another domain. Returns the policy under its new id.
	 */
	async move(id: string, domainKey: string) {
		const { data, error } = await tryExecute(
			this,
			Definitions.postUmbracoCspApiV1DefinitionsByIdMove({
				path: { id },
				query: { domainKey },
			}),
			{ disableNotifications: false }
		);

		if (data) {
			return { data };
		}

		return { error };
	}

	/**
	 * Save CSP definition. A domain policy posted with the empty id is created and gets its id from the server.
	 */
	async save(definition: CspApiDefinition) {
		const { data, error } = await tryExecute(
			this,
			Definitions.postUmbracoCspApiV1DefinitionsSave({
				body: withoutUnusedReporting(definition),
			}),
			{ disableNotifications: false }
		);

		if (data) {
			return { data };
		}

		return { error };
	}
}

/**
 * "No reporting" is no directive at all: the server only accepts report-uri or report-to. A URI
 * left over from a directive the user switched away from isn't sent either, so a stale value
 * can't fail validation for a setting that is off.
 */
export function withoutUnusedReporting(definition: CspApiDefinition): CspApiDefinition {
	const directive = definition.reportingDirective;
	if (directive && directive !== 'none') {
		return definition;
	}

	return { ...definition, reportingDirective: null, reportUri: null };
}

export { UmbCspDefinitionRepository as api };
