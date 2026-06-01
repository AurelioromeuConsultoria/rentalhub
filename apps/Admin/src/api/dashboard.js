import { api } from '@/lib/apiClient';

export const dashboardApi = {
  executivo: (params = {}) => api.get('/dashboard/executivo', { params }),
};
