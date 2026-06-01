import { api } from '@/lib/apiClient';

const defaultParams = { page: 1, pageSize: 50 };

export const usuariosApi = {
  list: (params = {}) => api.get('/usuarios', { params: { ...defaultParams, ...params } }),
  create: (payload) => api.post('/usuarios', payload),
  update: (id, payload) => api.put(`/usuarios/${id}`, payload),
  deactivate: (id) => api.delete(`/usuarios/${id}`),
};

export const perfisAcessoApi = {
  list: () => api.get('/perfis-acesso'),
};

export const configuracoesApi = {
  get: () => api.get('/configuracoes'),
  updateTenant: (payload) => api.put('/configuracoes/tenant', payload),
};
