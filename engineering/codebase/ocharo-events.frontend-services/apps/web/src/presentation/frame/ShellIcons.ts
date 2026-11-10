import type { Component } from 'vue';
import { BookOpen, CalendarDays, Globe, LayoutDashboard, Megaphone, Palette, PenLine, Shirt } from 'lucide-vue-next';

import type { OcharoAppKey } from '../../domain/shared/OcharoApps';

/** The icon on each app tile in the app menu, as Desk draws it; public sites share one. */
export const AppIcons: Readonly<Record<OcharoAppKey, Component>> = {
  desk: LayoutDashboard,
  catalogue: BookOpen,
  studio: Shirt,
  marketing: Megaphone,
  editorial: PenLine,
  brand: Palette,
  events: CalendarDays,
  atlas: Globe,
  wear: Globe,
  blog: Globe,
};
