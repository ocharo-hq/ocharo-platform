/**
 * The `localStorage` key Brand's colour mode lives under: the SDK `ColorModeProvider` persists `light`, `dark` or
 * `system` there, and the pre-paint `/theme.js` the build generates reads it before the first frame. Earlier builds
 * kept a fixed light palette and stored nothing, so no value needs migrating.
 */
export const ThemeStorageKey = 'ocharo.brand.theme';
