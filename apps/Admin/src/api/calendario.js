import { api } from '@/lib/apiClient';

export const calendarioApi = {
  events: (params) => api.get('/calendario', { params }),
  createBlock: (payload) => api.post('/calendario/bloqueios', payload),
  deleteBlock: (id) => api.delete(`/calendario/bloqueios/${id}`),
};
