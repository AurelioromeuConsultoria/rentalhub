const PREFIX = 'rentalhub:';

export const TOKEN_KEY = `${PREFIX}token`;
export const REFRESH_TOKEN_KEY = `${PREFIX}refreshToken`;
export const USER_KEY = `${PREFIX}usuario`;
export const SELECTED_TENANT_ID_KEY = `${PREFIX}selectedTenantId`;
export const SELECTED_TENANT_SLUG_KEY = `${PREFIX}selectedTenantSlug`;
export const SUPPORT_ACCESS_TOKEN_KEY = `${PREFIX}supportAccessToken`;
export const SUPPORT_ACCESS_REASON_KEY = `${PREFIX}supportAccessReason`;
export const SUPPORT_ACCESS_EXPIRES_KEY = `${PREFIX}supportAccessExpires`;
export const SUPPORT_ACCESS_TENANT_NAME_KEY = `${PREFIX}supportAccessTenantName`;

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
  localStorage.removeItem(SUPPORT_ACCESS_TOKEN_KEY);
  localStorage.removeItem(SUPPORT_ACCESS_REASON_KEY);
  localStorage.removeItem(SUPPORT_ACCESS_EXPIRES_KEY);
  localStorage.removeItem(SUPPORT_ACCESS_TENANT_NAME_KEY);
  clearLegacyAuthStorage();
}

export function clearSupportAccessStorage() {
  localStorage.removeItem(SELECTED_TENANT_ID_KEY);
  localStorage.removeItem(SELECTED_TENANT_SLUG_KEY);
  localStorage.removeItem(SUPPORT_ACCESS_TOKEN_KEY);
  localStorage.removeItem(SUPPORT_ACCESS_REASON_KEY);
  localStorage.removeItem(SUPPORT_ACCESS_EXPIRES_KEY);
  localStorage.removeItem(SUPPORT_ACCESS_TENANT_NAME_KEY);
}

export function readSupportAccessState(currentTenantId = null) {
  const selectedTenantId = localStorage.getItem(SELECTED_TENANT_ID_KEY);
  const selectedTenantSlug = localStorage.getItem(SELECTED_TENANT_SLUG_KEY);
  const token = localStorage.getItem(SUPPORT_ACCESS_TOKEN_KEY);
  const reason = localStorage.getItem(SUPPORT_ACCESS_REASON_KEY) || '';
  const expiresAt = localStorage.getItem(SUPPORT_ACCESS_EXPIRES_KEY);
  const tenantName = localStorage.getItem(SUPPORT_ACCESS_TENANT_NAME_KEY) || '';
  const expiresDate = expiresAt ? new Date(expiresAt) : null;
  const isExpired = Boolean(expiresDate && Number.isFinite(expiresDate.getTime()) && expiresDate <= new Date());
  const isCrossTenant = Boolean(
    selectedTenantId &&
    currentTenantId &&
    String(selectedTenantId) !== String(currentTenantId),
  );
  const isActive = Boolean(selectedTenantId && token && !isExpired && isCrossTenant);

  return {
    selectedTenantId,
    selectedTenantSlug,
    token,
    reason,
    expiresAt,
    tenantName,
    isExpired,
    isCrossTenant,
    isActive,
  };
}
