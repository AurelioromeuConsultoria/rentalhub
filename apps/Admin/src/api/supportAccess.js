import { api } from '@/lib/apiClient';

export const supportAccessApi = {
  start: (payload) => api.post('/support-access/start', payload),
  end: (token) => api.post('/support-access/end', { token }),
};
