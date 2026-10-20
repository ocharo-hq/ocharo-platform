import type { Component } from 'vue';
import {
  BookOpen,
  Boxes,
  Globe,
  Layers,
  LayoutDashboard,
  Megaphone,
  Palette,
  PenLine,
  Settings,
  Shirt,
} from 'lucide-vue-next';

import type { CatalogueSection } from '../../domain/shared/CatalogueSections';
import type { OcharoAppKey } from '../../domain/shared/OcharoApps';

/** The icon beside each Catalogue section in the sidebar, the rail and the phone's bottom bar. */
export const SectionIcons: Readonly<Record<CatalogueSection, Component>> = {
  catalogues: BookOpen,
  collections: Layers,
  products: Shirt,
  stock: Boxes,
  settings: Settings,
};

/** The icon on each app tile in the app menu, as Desk draws it; public sites share one. */
export const AppIcons: Readonly<Record<OcharoAppKey, Component>> = {
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
