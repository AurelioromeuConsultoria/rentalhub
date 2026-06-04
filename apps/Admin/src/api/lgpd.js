import { api } from '@/lib/apiClient';

export const lgpdApi = {
  status: () => api.get('/lgpd/status'),
  accept: (payload) => api.post('/lgpd/aceite', payload),
  exportData: (params) => api.get('/lgpd/exportar', { params }),
  anonymize: (payload) => api.post('/lgpd/anonimizar', payload),
};
