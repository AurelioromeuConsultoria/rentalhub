import { api } from '@/lib/apiClient';

const defaultParams = { page: 1, pageSize: 50 };

export const proprietariosApi = {
  list: (params = {}) => api.get('/proprietarios', { params: { ...defaultParams, ...params } }),
  create: (payload) => api.post('/proprietarios', payload),
  update: (id, payload) => api.put(`/proprietarios/${id}`, payload),
  deactivate: (id) => api.delete(`/proprietarios/${id}`),
};

export const hospedesApi = {
  list: (params = {}) => api.get('/hospedes', { params: { ...defaultParams, ...params } }),
  create: (payload) => api.post('/hospedes', payload),
  update: (id, payload) => api.put(`/hospedes/${id}`, payload),
  deactivate: (id) => api.delete(`/hospedes/${id}`),
};

export const imoveisApi = {
  list: (params = {}) => api.get('/imoveis', { params: { ...defaultParams, ...params } }),
  create: (payload) => api.post('/imoveis', payload),
  update: (id, payload) => api.put(`/imoveis/${id}`, payload),
  deactivate: (id) => api.delete(`/imoveis/${id}`),
};
