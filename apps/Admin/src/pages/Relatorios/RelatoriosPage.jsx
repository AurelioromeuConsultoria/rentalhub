import { Download, FileSpreadsheet, FileText, RotateCcw } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { categoriasFinanceirasApi } from '@/api/financeiro';
import { imoveisApi, proprietariosApi } from '@/api/cadastros';
import { relatoriosApi } from '@/api/relatorios';

const currentMonthStart = new Date();
currentMonthStart.setDate(1);

const tabs = [
  { key: 'reservas', label: 'Reservas' },
  { key: 'financeiro', label: 'Financeiro' },
  { key: 'imoveis', label: 'Imóveis' },
  { key: 'proprietarios', label: 'Sócios' },
];

const origemOptions = [
  { value: '', label: 'Todas' },
  { value: 1, label: 'Airbnb' },
  { value: 2, label: 'Booking' },
  { value: 3, label: 'VRBO' },
  { value: 4, label: 'Reserva Direta' },
  { value: 5, label: 'Outros' },
];

function extractItems(response) {
  return response.data?.items || response.data || [];
}

function money(value) {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(Number(value || 0));
}

function percent(value) {
  return `${Number(value || 0).toLocaleString('pt-BR', { maximumFractionDigits: 2 })}%`;
}

function getErrorMessage(error) {
  return error.response?.data?.message || 'Não foi possível carregar o relatório.';
}

function dateOnly(value) {
  if (!value) return '';
  return String(value).slice(0, 10);
}

function formatDate(value) {
  const normalized = dateOnly(value);
  if (!normalized) return '-';
  const [year, month, day] = normalized.split('-');
  return `${day}/${month}/${year}`;
}

function saveBlob(response, fallbackName) {
  const contentDisposition = response.headers?.['content-disposition'] || '';
  const match = contentDisposition.match(/filename="?([^"]+)"?/i);
  const fileName = match?.[1] || fallbackName;
  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function SummaryCard({ label, value }) {
  return (
    <article className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function ReportTable({ columns, items, emptyText }) {
  if (!items || items.length === 0) {
    return (
      <div className="inline-empty compact">
        <FileText size={24} />
        <strong>Sem dados no período</strong>
        <span>{emptyText}</span>
      </div>
    );
  }

  return (
    <div className="data-table-wrap">
      <table className="data-table">
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.key}>{column.label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {items.map((item, index) => (
            <tr key={item.id || item.imovelId || item.proprietarioId || index}>
              {columns.map((column) => (
                <td key={column.key}>{column.render ? column.render(item) : item[column.key]}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function RelatoriosPage() {
  const [activeTab, setActiveTab] = useState('reservas');
  const [filters, setFilters] = useState({
    inicio: currentMonthStart.toISOString().slice(0, 10),
    fim: new Date().toISOString().slice(0, 10),
    imovelId: '',
    proprietarioId: '',
    categoriaId: '',
    plataforma: '',
  });
  const [imoveis, setImoveis] = useState([]);
  const [proprietarios, setProprietarios] = useState([]);
  const [categorias, setCategorias] = useState([]);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [exportingFormat, setExportingFormat] = useState('');
  const [error, setError] = useState('');

  const baseParams = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
      imovelId: filters.imovelId || undefined,
      proprietarioId: filters.proprietarioId || undefined,
    }),
    [filters],
  );

  const reportParams = useMemo(() => {
    if (activeTab === 'proprietarios') {
      return {
        inicio: baseParams.inicio,
        fim: baseParams.fim,
        proprietarioId: baseParams.proprietarioId,
      };
    }

    if (activeTab === 'financeiro') {
      return {
        inicio: baseParams.inicio,
        fim: baseParams.fim,
        imovelId: baseParams.imovelId,
        categoriaId: filters.categoriaId || undefined,
      };
    }

    if (activeTab === 'reservas') {
      return {
        inicio: baseParams.inicio,
        fim: baseParams.fim,
        imovelId: baseParams.imovelId,
        plataforma: filters.plataforma || undefined,
      };
    }

    return {
      inicio: baseParams.inicio,
      fim: baseParams.fim,
      imovelId: baseParams.imovelId,
    };
  }, [activeTab, baseParams, filters.categoriaId, filters.plataforma]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const reportRequest = relatoriosApi[activeTab](reportParams);
      const [reportResponse, imoveisResponse, proprietariosResponse, categoriasResponse] = await Promise.all([
        reportRequest,
        imoveisApi.list({ status: 1, pageSize: 100 }),
        proprietariosApi.list({ ativo: true, pageSize: 100 }),
        categoriasFinanceirasApi.list({ ativo: true }),
      ]);
      setData(reportResponse.data);
      setImoveis(extractItems(imoveisResponse));
      setProprietarios(extractItems(proprietariosResponse));
      setCategorias(extractItems(categoriasResponse));
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [activeTab, reportParams]);

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, [load]);

  const exportReport = async (format) => {
    setExportingFormat(format);
    setError('');
    try {
      const exportMethod = relatoriosApi[`${activeTab}${format === 'pdf' ? 'Pdf' : 'Csv'}`];
      const response = await exportMethod(reportParams);
      saveBlob(response, `relatorio-${activeTab}.${format}`);
    } catch (exportError) {
      setError(getErrorMessage(exportError));
    } finally {
      setExportingFormat('');
    }
  };

  const items = data?.itens || [];

  const renderSummary = () => {
    if (activeTab === 'reservas') {
      return (
        <>
          <section className="kpi-grid secondary-kpis">
            <SummaryCard label="Reservas" value={data?.totalReservas || 0} />
            <SummaryCard label="Valor da hospedagem" value={money(data?.valorHospedagem)} />
            <SummaryCard label="Taxas de limpeza" value={money(data?.taxaLimpeza)} />
            <SummaryCard label="Valor líquido" value={money(data?.valorLiquido)} />
          </section>

          <section className="content-grid">
            <article className="panel">
              <div className="panel-heading">
                <h2>Origem das reservas</h2>
                <span>Distribuição</span>
              </div>
              {(data?.porPlataforma || []).length > 0 ? (
                <div className="origin-chart">
                  {data.porPlataforma.map((item) => {
                    const total = Number(data.totalReservas || 0);
                    const width = total > 0 ? (Number(item.quantidade || 0) / total) * 100 : 0;

                    return (
                      <article className="origin-row" key={item.nome}>
                        <div>
                          <strong>{item.nome}</strong>
                          <span>{item.quantidade} reservas</span>
                        </div>
                        <small>{money(item.total)}</small>
                        <div className="origin-bar">
                          <span style={{ width: `${Math.max(width, 4)}%` }} />
                        </div>
                      </article>
                    );
                  })}
                </div>
              ) : (
                <div className="inline-empty compact">
                  <FileText size={24} />
                  <strong>Sem distribuição</strong>
                  <span>Cadastre reservas para visualizar a origem por plataforma.</span>
                </div>
              )}
            </article>

            <article className="panel">
              <div className="panel-heading">
                <h2>Taxas e comissões</h2>
                <span>Descontos</span>
              </div>
              <div className="status-board">
                <div>
                  <small>Taxa da plataforma</small>
                  <strong>{money(data?.taxaPlataforma)}</strong>
                </div>
                <div>
                  <small>Comissão administradora</small>
                  <strong>{money(data?.comissaoAdministradora)}</strong>
                </div>
                <div>
                  <small>Total descontado</small>
                  <strong>{money(Number(data?.taxaPlataforma || 0) + Number(data?.comissaoAdministradora || 0))}</strong>
                </div>
              </div>
            </article>
          </section>
        </>
      );
    }

    if (activeTab === 'financeiro') {
      return (
        <>
          <section className="kpi-grid secondary-kpis">
            <SummaryCard label="Receitas" value={money(data?.receitas)} />
            <SummaryCard label="Despesas" value={money(data?.despesas)} />
            <SummaryCard label="Lucro" value={money(data?.lucro)} />
            <SummaryCard label="Categorias" value={data?.porCategoria?.length || 0} />
          </section>

          <section className="content-grid">
            <article className="panel">
              <div className="panel-heading">
                <h2>Categorias</h2>
                <span>Financeiro</span>
              </div>
              {(data?.porCategoria || []).length > 0 ? (
                <div className="performance-list">
                  {data.porCategoria.map((item) => {
                    const totalBase = item.tipo === 'Receita' ? Number(data.receitas || 0) : Number(data.despesas || 0);
                    const width = totalBase > 0 ? (Number(item.total || 0) / totalBase) * 100 : 0;

                    return (
                      <article className="performance-item" key={`${item.tipo}-${item.categoriaNome}`}>
                        <div>
                          <strong>{item.categoriaNome}</strong>
                          <span>{item.tipo}</span>
                        </div>
                        <div className="performance-value">
                          <strong>{money(item.total)}</strong>
                          <span>{totalBase > 0 ? `${width.toFixed(0)}% do total` : 'Sem base'}</span>
                        </div>
                        <div className="performance-bar">
                          <span style={{ width: `${Math.max(width, 4)}%` }} />
                        </div>
                      </article>
                    );
                  })}
                </div>
              ) : (
                <div className="inline-empty compact">
                  <FileText size={24} />
                  <strong>Sem categorias no período</strong>
                  <span>Ajuste os filtros ou registre movimentações financeiras.</span>
                </div>
              )}
            </article>
          </section>
        </>
      );
    }

    if (activeTab === 'imoveis') {
      return (
        <section className="kpi-grid secondary-kpis">
          <SummaryCard label="Receita" value={money(data?.receita)} />
          <SummaryCard label="Despesa" value={money(data?.despesa)} />
          <SummaryCard label="Lucro" value={money(data?.lucro)} />
          <SummaryCard label="Imóveis" value={items.length} />
        </section>
      );
    }

    return (
      <section className="kpi-grid secondary-kpis">
        <SummaryCard label="Receita" value={money(data?.receita)} />
        <SummaryCard label="Custos" value={money(data?.custos)} />
        <SummaryCard label="Repasses gerados" value={money(data?.repassesGerados)} />
        <SummaryCard label="Pendentes" value={money(data?.repassesPendentes)} />
      </section>
    );
  };

  const columns = {
    reservas: [
      { key: 'checkIn', label: 'Check-in', render: (item) => formatDate(item.checkIn) },
      { key: 'imovelNome', label: 'Imóvel' },
      { key: 'hospedeNome', label: 'Hóspede' },
      { key: 'plataforma', label: 'Plataforma' },
      { key: 'status', label: 'Status' },
      { key: 'valorLiquido', label: 'Líquido', render: (item) => money(item.valorLiquido) },
    ],
    financeiro: [
      { key: 'data', label: 'Data', render: (item) => formatDate(item.data) },
      { key: 'tipo', label: 'Tipo' },
      { key: 'categoriaNome', label: 'Categoria' },
      { key: 'imovelNome', label: 'Imóvel', render: (item) => item.imovelNome || '-' },
      { key: 'descricao', label: 'Descrição' },
      { key: 'valor', label: 'Valor', render: (item) => money(item.valor) },
    ],
    imoveis: [
      { key: 'imovelNome', label: 'Imóvel' },
      { key: 'receita', label: 'Receita', render: (item) => money(item.receita) },
      { key: 'despesa', label: 'Despesa', render: (item) => money(item.despesa) },
      { key: 'lucro', label: 'Lucro', render: (item) => money(item.lucro) },
      { key: 'reservas', label: 'Reservas' },
      { key: 'taxaOcupacao', label: 'Ocupação', render: (item) => percent(item.taxaOcupacao) },
    ],
    proprietarios: [
      { key: 'proprietarioNome', label: 'Sócio' },
      { key: 'totalImoveis', label: 'Imóveis' },
      { key: 'reservas', label: 'Reservas' },
      { key: 'receita', label: 'Receita', render: (item) => money(item.receita) },
      { key: 'custos', label: 'Custos', render: (item) => money(item.custos) },
      { key: 'repassesPendentes', label: 'Pendentes', render: (item) => money(item.repassesPendentes) },
    ],
  };

  return (
    <div className="resource-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Camada analítica</span>
          <h1>Relatórios</h1>
          <p>Reservas, financeiro, imóveis e sócios com totalizadores por período e exportação profissional.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
          <button className="secondary-action" type="button" onClick={() => exportReport('csv')} disabled={Boolean(exportingFormat)}>
            <FileSpreadsheet size={18} />
            {exportingFormat === 'csv' ? 'Gerando...' : 'CSV'}
          </button>
          <button className="primary-action" type="button" onClick={() => exportReport('pdf')} disabled={Boolean(exportingFormat)}>
            <Download size={18} />
            {exportingFormat === 'pdf' ? 'Gerando...' : 'PDF'}
          </button>
        </div>
      </section>

      <section className="resource-panel report-tabs">
        {tabs.map((tab) => (
          <button
            className={activeTab === tab.key ? 'active' : ''}
            key={tab.key}
            type="button"
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </section>

      <section className="resource-panel financial-filters">
        <label className="form-field">
          <span>Início</span>
          <input type="date" value={filters.inicio} onChange={(event) => setFilters((current) => ({ ...current, inicio: event.target.value }))} />
        </label>
        <label className="form-field">
          <span>Fim</span>
          <input type="date" value={filters.fim} onChange={(event) => setFilters((current) => ({ ...current, fim: event.target.value }))} />
        </label>
        {activeTab === 'proprietarios' ? (
          <label className="form-field">
            <span>Sócio</span>
            <select value={filters.proprietarioId} onChange={(event) => setFilters((current) => ({ ...current, proprietarioId: event.target.value }))}>
              <option value="">Todos</option>
              {proprietarios.map((proprietario) => (
                <option key={proprietario.id} value={proprietario.id}>
                  {proprietario.nome}
                </option>
              ))}
            </select>
          </label>
        ) : (
          <label className="form-field">
            <span>Imóvel</span>
            <select value={filters.imovelId} onChange={(event) => setFilters((current) => ({ ...current, imovelId: event.target.value }))}>
              <option value="">Todos</option>
              {imoveis.map((imovel) => (
                <option key={imovel.id} value={imovel.id}>
                  {imovel.nome}
                </option>
              ))}
            </select>
          </label>
        )}
        {activeTab === 'reservas' && (
          <label className="form-field">
            <span>Plataforma</span>
            <select value={filters.plataforma} onChange={(event) => setFilters((current) => ({ ...current, plataforma: event.target.value }))}>
              {origemOptions.map((option) => (
                <option key={String(option.value)} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
        )}
        {activeTab === 'financeiro' && (
          <label className="form-field">
            <span>Categoria</span>
            <select value={filters.categoriaId} onChange={(event) => setFilters((current) => ({ ...current, categoriaId: event.target.value }))}>
              <option value="">Todas</option>
              {categorias.map((categoria) => (
                <option key={categoria.id} value={categoria.id}>
                  {categoria.nome}
                </option>
              ))}
            </select>
          </label>
        )}
      </section>

      {error && <div className="form-alert">{error}</div>}
      {loading && <div className="loading-line">Carregando relatório...</div>}

      {renderSummary()}

      <section className="resource-panel">
        <div className="resource-panel-heading">
          <div>
            <strong>{tabs.find((tab) => tab.key === activeTab)?.label}</strong>
            <small>{items.length} linhas no período selecionado.</small>
          </div>
          <span>{items.length} registros</span>
        </div>
        <ReportTable columns={columns[activeTab]} items={items} emptyText="Ajuste os filtros ou cadastre dados operacionais." />
      </section>
    </div>
  );
}
