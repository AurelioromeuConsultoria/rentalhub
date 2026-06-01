import { api } from '@/lib/apiClient';

function exportCsv(path, params = {}) {
  return api.get(path, { params, responseType: 'blob' });
}

export const relatoriosApi = {
  reservas: (params = {}) => api.get('/relatorios/reservas', { params }),
  reservasCsv: (params = {}) => exportCsv('/relatorios/reservas.csv', params),
  financeiro: (params = {}) => api.get('/relatorios/financeiro', { params }),
  financeiroCsv: (params = {}) => exportCsv('/relatorios/financeiro.csv', params),
  imoveis: (params = {}) => api.get('/relatorios/imoveis', { params }),
  imoveisCsv: (params = {}) => exportCsv('/relatorios/imoveis.csv', params),
  proprietarios: (params = {}) => api.get('/relatorios/proprietarios', { params }),
  proprietariosCsv: (params = {}) => exportCsv('/relatorios/proprietarios.csv', params),
  demonstrativoRepasse: (id) => api.get(`/relatorios/repasses/${id}/demonstrativo`),
  demonstrativoRepassePdf: (id) => api.get(`/relatorios/repasses/${id}/demonstrativo.pdf`, { responseType: 'blob' }),
};
