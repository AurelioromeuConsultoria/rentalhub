import { api } from '@/lib/apiClient';

export const portalProprietarioApi = {
  get: (params = {}) => api.get('/portalproprietario', { params }),
  demonstrativoRepassePdf: (id) => api.get(`/portalproprietario/repasses/${id}/demonstrativo.pdf`, { responseType: 'blob' }),
};
