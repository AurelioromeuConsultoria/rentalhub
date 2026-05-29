import { api } from '@/lib/apiClient';

export const authApi = {
  login: (payload) => api.post('/auth/login', payload),
  me: () => api.get('/auth/me'),
  refresh: (payload) => api.post('/auth/refresh', payload),
};

