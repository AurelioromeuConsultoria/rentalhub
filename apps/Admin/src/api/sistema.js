import { api } from '@/lib/apiClient';

export const sistemaApi = {
  status: () => api.get('/sistema/status'),
};
