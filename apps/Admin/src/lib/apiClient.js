import axios from 'axios';
import { API_BASE_URL_WITH_API } from './env';

export const api = axios.create({
  baseURL: API_BASE_URL_WITH_API,
  headers: {
    'Content-Type': 'application/json',
  },
});

let refreshPromise = null;

function clearAuthSession() {
  localStorage.removeItem('token');
  localStorage.removeItem('refreshToken');
  localStorage.removeItem('usuario');
}

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  const usuario = JSON.parse(localStorage.getItem('usuario') || 'null');
  const selectedTenantId = localStorage.getItem('selectedTenantId');
  const selectedTenantSlug = localStorage.getItem('selectedTenantSlug');
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
        window.location.href = '/login';
        return Promise.reject(error);
      }

      const refreshToken = localStorage.getItem('refreshToken');
      if (!refreshToken) {
        clearAuthSession();
        window.location.href = '/login';
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
        localStorage.setItem('token', result.token);
        localStorage.setItem('refreshToken', result.refreshToken);
        localStorage.setItem('usuario', JSON.stringify(result.usuario));

        originalRequest.headers = originalRequest.headers || {};
        originalRequest.headers.Authorization = `Bearer ${result.token}`;
        return api(originalRequest);
      } catch (refreshError) {
        clearAuthSession();
        window.location.href = '/login';
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  },
);
