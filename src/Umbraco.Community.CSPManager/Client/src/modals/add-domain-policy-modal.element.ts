import { css, customElement, html, repeat, state } from '@umbraco-cms/backoffice/external/lit';
import { UmbModalBaseElement } from '@umbraco-cms/backoffice/modal';
import type { CspDomainInfo } from '@/api';
import { CspConstants } from '@/constants';
import { UmbCspDefinitionRepository } from '../repository/csp-definition.repository.js';
import type { AddDomainPolicyModalData, AddDomainPolicyModalValue } from './add-domain-policy-modal.token.js';

/**
 * Lists the Umbraco domains that don't have a policy yet. Picking one submits it straight away.
 * Used to add a domain policy and to move an orphaned one. Domain and content names are user
 * content, so they are only ever rendered as text bindings.
 */
@customElement('umb-csp-add-domain-policy-modal')
export class UmbCspAddDomainPolicyModalElement extends UmbModalBaseElement<
	AddDomainPolicyModalData,
	AddDomainPolicyModalValue
> {
	@state()
	private _domains: CspDomainInfo[] = [];

	@state()
	private _loading = true;

	@state()
	private _failed = false;

	override connectedCallback() {
		super.connectedCallback();
		this.#loadDomains();
	}

	async #loadDomains() {
		const { data, error } = await new UmbCspDefinitionRepository(this).getDomains();
		this._loading = false;

		if (error || !data) {
			this._failed = true;
			return;
		}

		this._domains = data.filter((domain) => !domain.hasCspPolicy);
	}

	#select(domain: CspDomainInfo) {
		this.value = { domainKey: domain.key };
		this.modalContext?.submit();
	}

	#cancel() {
		this.modalContext?.reject();
	}

	#detail(domain: CspDomainInfo) {
		const culture = domain.culture || this.localize.term('cspManagerDomainPolicy_unknownCulture');
		const content = domain.rootContentName || this.localize.term('cspManagerDomainPolicy_unknownContent');
		return `${culture} · ${content}`;
	}

	override render() {
		return html`
			<umb-body-layout headline=${this.localize.term(this.data?.headlineKey ?? 'cspManagerDomainPolicy_addModalHeadline')}>
				<uui-box>${this.#renderContent()}</uui-box>
				<div slot="actions">
					<uui-button label=${this.localize.term('general_cancel')} @click=${this.#cancel}></uui-button>
				</div>
			</umb-body-layout>
		`;
	}

	#renderContent() {
		if (this._loading) {
			return html`<div class="loader"><uui-loader></uui-loader></div>`;
		}

		if (this._failed) {
			return html`<p class="error">${this.localize.term('cspManagerDomainPolicy_addModalLoadFailed')}</p>`;
		}

		if (this._domains.length === 0) {
			return html`<p class="empty" data-mark="csp-no-domains">${this.localize.term('cspManagerDomainPolicy_addModalEmpty')}</p>`;
		}

		return html`
			<uui-ref-list>
				${repeat(
					this._domains,
					(domain) => domain.key,
					(domain) => html`
						<uui-ref-node
							name=${domain.name}
							detail=${this.#detail(domain)}
							data-mark="csp-domain:${domain.name}"
							@open=${() => this.#select(domain)}>
							<uui-icon slot="icon" name=${CspConstants.icons.domainPolicy}></uui-icon>
						</uui-ref-node>
					`,
				)}
			</uui-ref-list>
		`;
	}

	static override styles = [
		css`
			.loader {
				display: flex;
				justify-content: center;
				padding: var(--uui-size-space-5);
			}

			p {
				margin-top: 0;
				color: var(--uui-color-text-alt);
			}

			.error {
				color: var(--uui-color-danger);
			}
		`,
	];
}

export default UmbCspAddDomainPolicyModalElement;

declare global {
	interface HTMLElementTagNameMap {
		'umb-csp-add-domain-policy-modal': UmbCspAddDomainPolicyModalElement;
	}
}
