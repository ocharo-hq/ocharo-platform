import type { Component } from 'vue';
import {
  BookOpen,
  CalendarDays,
  Contact,
  FileText,
  Globe,
  House,
  Images,
  LayoutDashboard,
  Megaphone,
  Package,
  Palette,
  PenLine,
  Plug,
  Shirt,
  SlidersHorizontal,
  Users,
} from 'lucide-vue-next';

import type { DeskSection } from '@/domain/workspace/DeskSections';
import type { OcharoApp } from '@/domain/shared/OcharoApps';

/** The icon beside each Desk section in the sidebar, the rail and the phone's bottom bar. */
export const SectionIcons: Readonly<Record<DeskSection, Component>> = {
  overview: House,
  events: CalendarDays,
  sourcing: Users,
  products: Package,
  catalogue: BookOpen,
  media: Images,
  brands: Palette,
  'business-cards': Contact,
  content: FileText,
  integrations: Plug,
  system: SlidersHorizontal,
};

/** The icon on each app tile in the app menu; public sites share one. */
export const AppIcons: Readonly<Record<OcharoApp['key'], Component>> = {
  desk: LayoutDashboard,
  catalogue: BookOpen,
  studio: Shirt,
  marketing: Megaphone,
  editorial: PenLine,
  brand: Palette,
  atlas: Globe,
  wear: Globe,
  blog: Globe,
};
