import { api } from '@/lib/apiClient';

const defaultParams = { page: 1, pageSize: 50 };

export const reservasApi = {
  list: (params = {}) => api.get('/reservas', { params: { ...defaultParams, ...params } }),
  create: (payload) => api.post('/reservas', payload),
  update: (id, payload) => api.put(`/reservas/${id}`, payload),
  cancel: (id) => api.delete(`/reservas/${id}`),
  availability: (params) => api.get('/reservas/disponibilidade', { params }),
};
