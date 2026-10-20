/**
 * The `localStorage` key Catalogue's colour mode lives under: the SDK `ColorModeProvider` persists `light`, `dark` or
 * `system` there, and the pre-paint `/theme.js` the build generates reads it before the first frame. Earlier builds
 * stored the same three values under this key, so a remembered choice carries over unchanged.
 */
export const ThemeStorageKey = 'ocharo.catalogue.theme';
