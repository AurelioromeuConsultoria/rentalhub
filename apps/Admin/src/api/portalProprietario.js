import { api } from '@/lib/apiClient';

export const portalProprietarioApi = {
  get: (params = {}) => api.get('/portalproprietario', { params }),
  reservasPdf: (params = {}) => api.get('/portalproprietario/reservas.pdf', { params, responseType: 'blob' }),
  reservaPdf: (id) => api.get(`/portalproprietario/reservas/${id}/detalhe.pdf`, { responseType: 'blob' }),
  movimentacoesPdf: (params = {}) => api.get('/portalproprietario/movimentacoes.pdf', { params, responseType: 'blob' }),
  repassesPdf: (params = {}) => api.get('/portalproprietario/repasses.pdf', { params, responseType: 'blob' }),
  demonstrativoMensalPdf: (params = {}) => api.get('/portalproprietario/demonstrativo-mensal.pdf', { params, responseType: 'blob' }),
  repasseDetalhe: (id) => api.get(`/portalproprietario/repasses/${id}`),
  demonstrativoRepassePdf: (id) => api.get(`/portalproprietario/repasses/${id}/demonstrativo.pdf`, { responseType: 'blob' }),
};
