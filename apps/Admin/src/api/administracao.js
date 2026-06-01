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
  create: (payload) => api.post('/perfis-acesso', payload),
  update: (id, payload) => api.put(`/perfis-acesso/${id}`, payload),
  deactivate: (id) => api.delete(`/perfis-acesso/${id}`),
};

export const configuracoesApi = {
  get: () => api.get('/configuracoes'),
  updateTenant: (payload) => api.put('/configuracoes/tenant', payload),
};

export const tenantsApi = {
  list: () => api.get('/tenants'),
  create: (payload) => api.post('/tenants', payload),
  update: (id, payload) => api.put(`/tenants/${id}`, payload),
  deactivate: (id) => api.delete(`/tenants/${id}`),
};

export const auditoriaApi = {
  list: (params = {}) => api.get('/auditoria', { params: { ...defaultParams, pageSize: 30, ...params } }),
};
