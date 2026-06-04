import { api } from '@/lib/apiClient';

export const suporteApi = {
  list: (params = {}) => api.get('/suporte', { params }),
  create: (payload) => api.post('/suporte', payload),
  updateStatus: (id, payload) => api.put(`/suporte/${id}/status`, payload),
};
