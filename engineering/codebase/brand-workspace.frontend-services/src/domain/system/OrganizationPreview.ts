/** Describes planned first-account context only; this is not a persisted organization or session. */
export interface OrganizationPreview {
  readonly planned: true;
  readonly name: string;
  readonly description: string;
}
