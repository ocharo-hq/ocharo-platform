import type { Component } from 'vue';
import {
  BookOpen,
  CalendarHeart,
  Globe,
  Image,
  LayoutDashboard,
  Megaphone,
  Palette,
  PenLine,
  Plug,
  Shirt,
} from 'lucide-vue-next';

import type { BrandSection } from '../../domain/shared/BrandSections';
import type { OcharoAppKey } from '../../domain/shared/OcharoApps';

/** The icon beside each Brand section in the sidebar, the rail and the phone's bottom bar. */
export const SectionIcons: Readonly<Record<BrandSection, Component>> = {
  overview: LayoutDashboard,
  identity: Palette,
  logos: Image,
  appearances: CalendarHeart,
  connect: Plug,
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
