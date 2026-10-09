/** Public account-access state; bot tokens and sign-in codes are never persisted in this model. */
export interface TelegramAvailability { enabled: boolean; botUsername: string | null; linked: boolean; phone: string | null }
export interface TelegramLink { botUrl: string; expiresAt: string }
export interface TelegramCode { expiresAt: string }
