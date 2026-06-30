import axios from 'axios';
import {
  clearAuthStorage,
  clearSupportAccessStorage,
  REFRESH_TOKEN_KEY,
  TOKEN_KEY,
  USER_KEY,
  readSupportAccessState,
} from './authStorage';
import { API_BASE_URL_WITH_API } from './env';
import { isPlatformAdminUser } from './platformAccess';

export const api = axios.create({
  baseURL: API_BASE_URL_WITH_API,
  headers: {
    'Content-Type': 'application/json',
  },
});

let refreshPromise = null;

function clearAuthSession() {
  clearAuthStorage();
}

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY);
  const usuario = JSON.parse(localStorage.getItem(USER_KEY) || 'null');
  const supportAccess = readSupportAccessState(usuario?.tenantId);
  const requestUrl = String(config.url || '').toLowerCase();
  const isAuthRequest =
    requestUrl.includes('/auth/login') ||
    requestUrl.includes('/auth/me') ||
    requestUrl.includes('/auth/refresh');

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  if (!isAuthRequest && isPlatformAdminUser(usuario) && supportAccess.selectedTenantId) {
    if (supportAccess.isExpired) {
      clearSupportAccessStorage();
      return config;
    }

    config.headers['X-Tenant-Id'] = supportAccess.selectedTenantId;
    if (supportAccess.selectedTenantSlug) {
      config.headers['X-Tenant-Slug'] = supportAccess.selectedTenantSlug;
    }

    if (supportAccess.isActive && supportAccess.token) {
      config.headers['X-Support-Access-Token'] = supportAccess.token;
    }
  }

  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config || {};
    const requestUrl = String(originalRequest.url || '').toLowerCase();
    const isLoginRequest = requestUrl.includes('/auth/login');
    const isRefreshRequest = requestUrl.includes('/auth/refresh');

    if (error.response?.status === 401 && !isLoginRequest) {
      if (isRefreshRequest || originalRequest._retry) {
        clearAuthSession();
        window.location.assign('/login');
        return Promise.reject(error);
      }

      const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
      if (!refreshToken) {
        clearAuthSession();
        window.location.assign('/login');
        return Promise.reject(error);
      }

      originalRequest._retry = true;

      try {
        if (!refreshPromise) {
          refreshPromise = api
            .post('/auth/refresh', { refreshToken })
            .then((response) => response.data)
            .finally(() => {
              refreshPromise = null;
            });
        }

        const result = await refreshPromise;
        localStorage.setItem(TOKEN_KEY, result.token);
        localStorage.setItem(REFRESH_TOKEN_KEY, result.refreshToken);
        localStorage.setItem(USER_KEY, JSON.stringify(result.usuario));

        originalRequest.headers = originalRequest.headers || {};
        originalRequest.headers.Authorization = `Bearer ${result.token}`;
        return api(originalRequest);
      } catch (refreshError) {
        clearAuthSession();
        window.location.assign('/login');
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  },
);
