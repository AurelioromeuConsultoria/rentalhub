import { api } from '@/lib/apiClient';

export const portalProprietarioApi = {
  get: (params = {}) => api.get('/portalproprietario', { params }),
};
