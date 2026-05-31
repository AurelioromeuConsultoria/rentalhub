import { api } from '@/lib/apiClient';

const defaultParams = { page: 1, pageSize: 50 };

export const categoriasFinanceirasApi = {
  list: (params = {}) => api.get('/categoriasfinanceiras', { params }),
  create: (payload) => api.post('/categoriasfinanceiras', payload),
  update: (id, payload) => api.put(`/categoriasfinanceiras/${id}`, payload),
  deactivate: (id) => api.delete(`/categoriasfinanceiras/${id}`),
};

export const financeiroApi = {
  listMovimentacoes: (params = {}) => api.get('/financeiro/movimentacoes', { params: { ...defaultParams, ...params } }),
  createMovimentacao: (payload) => api.post('/financeiro/movimentacoes', payload),
  updateMovimentacao: (id, payload) => api.put(`/financeiro/movimentacoes/${id}`, payload),
  deleteMovimentacao: (id) => api.delete(`/financeiro/movimentacoes/${id}`),
  fluxoCaixa: (params = {}) => api.get('/financeiro/fluxo-caixa', { params }),
};
