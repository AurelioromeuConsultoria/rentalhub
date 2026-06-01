import axios from 'axios';
import {
  clearAuthStorage,
  REFRESH_TOKEN_KEY,
  SELECTED_TENANT_ID_KEY,
  SELECTED_TENANT_SLUG_KEY,
  TOKEN_KEY,
  USER_KEY,
} from './authStorage';
import { API_BASE_URL_WITH_API } from './env';

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
  const selectedTenantId = localStorage.getItem(SELECTED_TENANT_ID_KEY);
  const selectedTenantSlug = localStorage.getItem(SELECTED_TENANT_SLUG_KEY);
  const requestUrl = String(config.url || '').toLowerCase();
  const isAuthRequest =
    requestUrl.includes('/auth/login') ||
    requestUrl.includes('/auth/me') ||
    requestUrl.includes('/auth/refresh');

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  if (!isAuthRequest && usuario?.isPlatformAdmin && selectedTenantId) {
    config.headers['X-Tenant-Id'] = selectedTenantId;
    if (selectedTenantSlug) {
      config.headers['X-Tenant-Slug'] = selectedTenantSlug;
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
