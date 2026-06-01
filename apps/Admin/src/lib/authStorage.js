const PREFIX = 'rentalhub:';

export const TOKEN_KEY = `${PREFIX}token`;
export const REFRESH_TOKEN_KEY = `${PREFIX}refreshToken`;
export const USER_KEY = `${PREFIX}usuario`;
export const SELECTED_TENANT_ID_KEY = `${PREFIX}selectedTenantId`;
export const SELECTED_TENANT_SLUG_KEY = `${PREFIX}selectedTenantSlug`;

const legacyKeys = [
  'token',
  'refreshToken',
  'usuario',
  'selectedTenantId',
  'selectedTenantSlug',
];

export function clearLegacyAuthStorage() {
  legacyKeys.forEach((key) => localStorage.removeItem(key));
}

export function clearAuthStorage() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  localStorage.removeItem(SELECTED_TENANT_ID_KEY);
  localStorage.removeItem(SELECTED_TENANT_SLUG_KEY);
  clearLegacyAuthStorage();
}
