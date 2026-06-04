import { History, RotateCcw, Search } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { auditoriaApi } from '@/api/administracao';

const today = new Date().toISOString().slice(0, 10);
const monthStart = new Date();
monthStart.setDate(1);

function formatDateTime(value) {
  if (!value) return '-';

  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value));
}

function actionClass(action) {
  const normalized = String(action || '').toLowerCase();
  if (normalized.includes('criado')) return 'active';
  if (normalized.includes('excluido')) return 'inactive';
  return '';
}

export function AuditoriaPage() {
  const [logs, setLogs] = useState([]);
  const [pagination, setPagination] = useState({ totalItems: 0, page: 1, totalPages: 1 });
  const [filters, setFilters] = useState({
    inicio: monthStart.toISOString().slice(0, 10),
    fim: today,
    entidade: '',
    acao: '',
    usuario: '',
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const params = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
      entidade: filters.entidade || undefined,
      acao: filters.acao || undefined,
      usuario: filters.usuario || undefined,
      page: pagination.page,
    }),
    [filters, pagination.page],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError('');

    try {
      const response = await auditoriaApi.list(params);
      setLogs(response.data?.items || []);
      setPagination((current) => ({
        ...current,
        totalItems: response.data?.totalItems || 0,
        totalPages: response.data?.totalPages || 1,
      }));
    } catch (loadError) {
      setError(loadError.response?.data?.message || 'Não foi possível carregar a auditoria.');
      setLogs([]);
    } finally {
      setLoading(false);
    }
  }, [params]);

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, [load]);

  const updateFilter = (field, value) => {
    setFilters((current) => ({ ...current, [field]: value }));
    setPagination((current) => ({ ...current, page: 1 }));
  };

  const resetFilters = () => {
    setFilters({
      inicio: monthStart.toISOString().slice(0, 10),
      fim: today,
      entidade: '',
      acao: '',
      usuario: '',
    });
    setPagination((current) => ({ ...current, page: 1 }));
  };

  return (
    <section className="resource-page">
      <div className="page-heading">
        <div>
          <span className="eyebrow">Administração</span>
          <h1>Auditoria</h1>
          <p>Histórico de ações da empresa para rastrear alterações operacionais e administrativas.</p>
        </div>
        <button className="icon-button bordered" type="button" onClick={load} title="Atualizar">
          <RotateCcw size={18} />
        </button>
      </div>

      <div className="resource-panel">
        <div className="audit-filters">
          <label className="form-field">
            <span>Início</span>
            <input type="date" value={filters.inicio} onChange={(event) => updateFilter('inicio', event.target.value)} />
          </label>
          <label className="form-field">
            <span>Fim</span>
            <input type="date" value={filters.fim} onChange={(event) => updateFilter('fim', event.target.value)} />
          </label>
          <label className="form-field">
            <span>Entidade</span>
            <input value={filters.entidade} onChange={(event) => updateFilter('entidade', event.target.value)} />
          </label>
          <label className="form-field">
            <span>Ação</span>
            <input value={filters.acao} onChange={(event) => updateFilter('acao', event.target.value)} />
          </label>
          <label className="form-field">
            <span>Usuário</span>
            <input value={filters.usuario} onChange={(event) => updateFilter('usuario', event.target.value)} />
          </label>
          <button className="icon-button bordered" type="button" onClick={resetFilters} title="Limpar filtros">
            <Search size={18} />
          </button>
        </div>
      </div>

      <div className="resource-panel">
        <div className="resource-panel-heading">
          <strong className="form-title"><History size={18} /> Eventos</strong>
          <span>{pagination.totalItems} registro(s)</span>
        </div>

        {error && <div className="form-alert">{error}</div>}
        {loading ? (
          <div className="loading-line">Carregando auditoria...</div>
        ) : logs.length === 0 ? (
          <div className="inline-empty compact">
            <strong>Nenhum evento encontrado</strong>
            <span>Ajuste os filtros ou realize alguma ação operacional para gerar novos registros.</span>
          </div>
        ) : (
          <div className="data-table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Data</th>
                  <th>Ação</th>
                  <th>Entidade</th>
                  <th>Usuário</th>
                  <th>Origem</th>
                </tr>
              </thead>
              <tbody>
                {logs.map((log) => (
                  <tr key={log.id}>
                    <td>{formatDateTime(log.createdAt)}</td>
                    <td><span className={`status-pill ${actionClass(log.action)}`}>{log.action}</span></td>
                    <td>
                      <strong>{log.entityName}</strong>
                      <small>#{log.entityId || '-'}</small>
                    </td>
                    <td>
                      <strong>{log.userName || 'Sistema'}</strong>
                      <small>{log.userEmail || '-'}</small>
                    </td>
                    <td>{log.ipAddress || '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="pagination-row">
          <button
            className="icon-button bordered"
            type="button"
            disabled={pagination.page <= 1}
            onClick={() => setPagination((current) => ({ ...current, page: current.page - 1 }))}
          >
            Anterior
          </button>
          <span>Página {pagination.page} de {pagination.totalPages}</span>
          <button
            className="icon-button bordered"
            type="button"
            disabled={pagination.page >= pagination.totalPages}
            onClick={() => setPagination((current) => ({ ...current, page: current.page + 1 }))}
          >
            Próxima
          </button>
        </div>
      </div>
    </section>
  );
}
