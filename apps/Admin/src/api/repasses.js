import { api } from '@/lib/apiClient';

const defaultParams = { page: 1, pageSize: 50 };

export const repassesApi = {
  list: (params = {}) => api.get('/repasses', { params: { ...defaultParams, ...params } }),
  gerar: (payload) => api.post('/repasses/gerar', payload),
  registrarPagamento: (id, payload) => api.post(`/repasses/${id}/pagamentos`, payload),
  delete: (id) => api.delete(`/repasses/${id}`),
};
