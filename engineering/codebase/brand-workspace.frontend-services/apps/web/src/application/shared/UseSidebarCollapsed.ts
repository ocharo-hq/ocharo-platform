import { ref, watch, type Ref } from 'vue';

const storageKey = 'ocharo.desk.sidebar-collapsed';
let collapsed: Ref<boolean> | undefined;

/** Whether the desktop sidebar shows as the icon rail; remembered in this browser, shared by every frame instance. */
export function useSidebarCollapsed(): Ref<boolean> {
  if (collapsed) return collapsed;
  let saved = false;
  try { saved = localStorage.getItem(storageKey) === 'true'; } catch { /* Storage is optional in restricted browser modes. */ }
  const state = ref(saved);
  watch(state, (value) => {
    try { localStorage.setItem(storageKey, String(value)); } catch { /* The choice still holds for this tab. */ }
  });
  collapsed = state;
  return state;
}
