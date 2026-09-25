import type { OrganizationPreview } from '@/domain/system/OrganizationPreview';

/** Supplies deployment-specific preview copy without creating an account or an organization. */
export const organizationPreview: OrganizationPreview = {
  planned: true,
  name: 'Ocharo',
  description: 'Built with Maftuna’s work in mind, on a foundation other brands can share. Organization setup comes next.',
};
