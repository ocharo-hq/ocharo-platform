import type { Component } from 'vue';
import {
  BellRing,
  BookOpen,
  CalendarDays,
  Globe,
  LayoutDashboard,
  Megaphone,
  Newspaper,
  Palette,
  PenLine,
  Send,
  Shirt,
} from 'lucide-vue-next';

import type { EventsSection } from '../../domain/shared/EventsSections';
import type { OcharoAppKey } from '../../domain/shared/OcharoApps';

/** The icon beside each Events section in the sidebar, the rail and the phone's bottom bar. */
export const SectionIcons: Readonly<Record<EventsSection, Component>> = {
  feed: Newspaper,
  calendar: CalendarDays,
  reminders: BellRing,
  telegram: Send,
};

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
