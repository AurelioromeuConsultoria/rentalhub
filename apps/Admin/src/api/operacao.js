import { api } from '@/lib/apiClient';

export const notificacoesApi = {
  list: (params = {}) => api.get('/notificacoes', { params }),
};

export const buscaGlobalApi = {
  search: (params = {}) => api.get('/buscaglobal', { params }),
};
