/**
 * The first-load splash's logo and stages, shared by the mounted `SplashScreen` and its static twin in `index.html`
 * (styled by `public/boot-splash.css`): the Ocharo wordmark, ink on the light canvas and reverse on the dark one.
 */
export const SplashLogo = {
  ink: `${import.meta.env.BASE_URL}brand/ocharo-wordmark-ink.png`,
  reverse: `${import.meta.env.BASE_URL}brand/ocharo-wordmark-reverse.png`,
  width: 240,
  height: 90,
} as const;

/** The boot stages the splash bar counts: the shell mounting, the session settling and the first place resolving. */
export const BootStageCount = 3;
