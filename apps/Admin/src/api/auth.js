import { api } from '@/lib/apiClient';

export const authApi = {
  login: (payload) => api.post('/auth/login', payload),
  me: () => api.get('/auth/me'),
  refresh: (payload) => api.post('/auth/refresh', payload),
  forgotPassword: (payload) => api.post('/auth/esqueci-senha', payload),
  setPassword: (payload) => api.post('/auth/definir-senha', payload),
};
