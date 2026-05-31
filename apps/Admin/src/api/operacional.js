import { api } from '@/lib/apiClient';

const defaultParams = { page: 1, pageSize: 50 };

export const limpezasApi = {
  list: (params = {}) => api.get('/limpezas', { params: { ...defaultParams, ...params } }),
  create: (payload) => api.post('/limpezas', payload),
  update: (id, payload) => api.put(`/limpezas/${id}`, payload),
  cancel: (id) => api.delete(`/limpezas/${id}`),
};

export const manutencoesApi = {
  list: (params = {}) => api.get('/manutencoes', { params: { ...defaultParams, ...params } }),
  create: (payload) => api.post('/manutencoes', payload),
  update: (id, payload) => api.put(`/manutencoes/${id}`, payload),
  cancel: (id) => api.delete(`/manutencoes/${id}`),
};
