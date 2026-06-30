import axios from 'axios';
import { api } from '@/lib/apiClient';
import { API_BASE_URL_WITH_API } from '@/lib/env';

const publicApi = axios.create({
  baseURL: API_BASE_URL_WITH_API,
});

export const preCheckinsApi = {
  list: () => api.get('/precheckins'),
  detail: (id) => api.get(`/precheckins/${id}`),
  generateLink: (reservaId) => api.post(`/precheckins/reservas/${reservaId}/link`),
  approve: (id) => api.post(`/precheckins/${id}/aprovar`),
  reject: (id, motivo) => api.post(`/precheckins/${id}/reprovar`, { motivo }),
};

export const publicPreCheckinsApi = {
  get: (token) => publicApi.get(`/precheckins/public/${token}`),
  uploadPhoto: (token, file) => {
    const formData = new FormData();
    formData.append('arquivo', file);
    return publicApi.post(`/precheckins/public/${token}/fotos/upload`, formData);
  },
  submit: (token, payload) => publicApi.post(`/precheckins/public/${token}`, payload),
};
