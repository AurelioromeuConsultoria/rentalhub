import {
  ArrowDownRight,
  ArrowUpRight,
  BarChart3,
  CalendarCheck,
  Home,
  PieChart,
  RotateCcw,
  Sparkles,
  WalletCards,
  Wrench,
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { dashboardApi } from '@/api/dashboard';

const currentMonthStart = new Date();
currentMonthStart.setDate(1);

const emptyDashboard = {
  receitaMes: 0,
  despesaMes: 0,
  lucroMes: 0,
  reservasMes: 0,
  taxaOcupacao: 0,
  ticketMedio: 0,
  imoveisAtivos: 0,
  repassesPendentes: 0,
  limpezasPendentes: 0,
  manutencoesPendentes: 0,
  fluxoDiario: [],
  reservasPorOrigem: [],
  imoveisMaisRentaveis: [],
  imoveisMenorDesempenho: [],
};

function money(value) {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(Number(value || 0));
}

function percent(value) {
  return `${Number(value || 0).toLocaleString('pt-BR', { maximumFractionDigits: 2 })}%`;
}

function getErrorMessage(error) {
  return error.response?.data?.message || 'Não foi possível carregar o dashboard.';
}

function shortDate(value) {
  if (!value) return '';
  const [, month, day] = String(value).slice(0, 10).split('-');
  return `${day}/${month}`;
}

function FlowChart({ items }) {
  const maxValue = Math.max(
    ...items.map((item) => Math.max(Number(item.receita || 0), Number(item.despesa || 0))),
    1,
  );

  return (
    <article className="panel">
      <div className="panel-heading">
        <h2>Fluxo do período</h2>
        <span>Receita x despesa</span>
      </div>
      {items.length === 0 ? (
        <div className="inline-empty compact">
          <BarChart3 size={24} />
          <strong>Sem movimentações</strong>
          <span>Registre receitas e despesas para montar o gráfico.</span>
        </div>
      ) : (
        <div className="flow-chart" aria-label="Fluxo diário de receitas e despesas">
          {items.map((item) => (
            <div className="flow-day" key={item.data}>
              <div className="flow-bars">
                <span
                  className="income"
                  title={`Receita ${money(item.receita)}`}
                  style={{ height: `${Math.max(3, (Number(item.receita || 0) / maxValue) * 100)}%` }}
                />
                <span
                  className="expense"
                  title={`Despesa ${money(item.despesa)}`}
                  style={{ height: `${Math.max(3, (Number(item.despesa || 0) / maxValue) * 100)}%` }}
                />
              </div>
              <small>{shortDate(item.data)}</small>
            </div>
          ))}
        </div>
      )}
    </article>
  );
}

function OriginChart({ items }) {
  const total = items.reduce((sum, item) => sum + Number(item.quantidade || 0), 0);

  return (
    <article className="panel">
      <div className="panel-heading">
        <h2>Origem das reservas</h2>
        <span>Canais</span>
      </div>
      {items.length === 0 ? (
        <div className="inline-empty compact">
          <PieChart size={24} />
          <strong>Sem reservas</strong>
          <span>As origens aparecem conforme as reservas entram.</span>
        </div>
      ) : (
        <div className="origin-chart">
          {items.map((item) => {
            const percentage = total === 0 ? 0 : Math.round((Number(item.quantidade || 0) / total) * 100);

            return (
              <div className="origin-row" key={item.origem}>
                <div>
                  <strong>{item.origem}</strong>
                  <span>{item.quantidade} reservas · {money(item.receita)}</span>
                </div>
                <small>{percentage}%</small>
                <span className="origin-bar">
                  <span style={{ width: `${Math.max(4, percentage)}%` }} />
                </span>
              </div>
            );
          })}
        </div>
      )}
    </article>
  );
}

function PerformanceList({ title, badge, items, emptyText }) {
  return (
    <article className="panel">
      <div className="panel-heading">
        <h2>{title}</h2>
        <span>{badge}</span>
      </div>
      {items.length === 0 ? (
        <div className="inline-empty compact">
          <Home size={24} />
          <strong>Sem dados no período</strong>
          <span>{emptyText}</span>
        </div>
      ) : (
        <div className="performance-list">
          {items.map((item) => {
            const maxValue = Math.max(...items.map((current) => Math.abs(Number(current.lucro || 0))), 1);
            const width = Math.max(6, Math.round((Math.abs(Number(item.lucro || 0)) / maxValue) * 100));

            return (
              <div className="performance-item" key={item.imovelId}>
                <div>
                  <strong>{item.imovelNome}</strong>
                  <span>
                    {item.reservas} reservas · {item.noitesOcupadas} noites
                  </span>
                </div>
                <div className="performance-value">
                  <strong>{money(item.lucro)}</strong>
                  <span>Receita {money(item.receita)}</span>
                </div>
                <span className="performance-bar">
                  <span style={{ width: `${width}%` }} />
                </span>
              </div>
            );
          })}
        </div>
      )}
    </article>
  );
}

export function Dashboard() {
  const [filters, setFilters] = useState({
    inicio: currentMonthStart.toISOString().slice(0, 10),
    fim: new Date().toISOString().slice(0, 10),
  });
  const [data, setData] = useState(emptyDashboard);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const params = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
    }),
    [filters],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const response = await dashboardApi.executivo(params);
      setData(response.data || emptyDashboard);
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [params]);

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, [load]);

  const kpis = [
    { label: 'Receita do período', value: money(data.receitaMes), icon: ArrowUpRight, tone: 'green' },
    { label: 'Despesa do período', value: money(data.despesaMes), icon: ArrowDownRight, tone: 'red' },
    { label: 'Lucro do período', value: money(data.lucroMes), icon: WalletCards, tone: 'blue' },
    { label: 'Reservas do período', value: String(data.reservasMes || 0), icon: CalendarCheck, tone: 'yellow' },
  ];

  return (
    <div className="dashboard-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Dashboard executivo</span>
          <h1>Visão geral</h1>
          <p>Indicadores operacionais e financeiros calculados a partir das reservas, caixa, repasses e pendências.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
        </div>
      </section>

      <section className="resource-panel dashboard-filters">
        <label className="form-field">
          <span>Início</span>
          <input type="date" value={filters.inicio} onChange={(event) => setFilters((current) => ({ ...current, inicio: event.target.value }))} />
        </label>
        <label className="form-field">
          <span>Fim</span>
          <input type="date" value={filters.fim} onChange={(event) => setFilters((current) => ({ ...current, fim: event.target.value }))} />
        </label>
      </section>

      {error && <div className="form-alert">{error}</div>}
      {loading && <div className="loading-line">Carregando indicadores...</div>}

      <section className="kpi-grid" aria-label="Indicadores executivos">
        {kpis.map((kpi) => {
          const Icon = kpi.icon;

          return (
            <article className="metric-card" key={kpi.label}>
              <div className={`metric-icon ${kpi.tone}`}>
                <Icon size={20} />
              </div>
              <span>{kpi.label}</span>
              <strong>{kpi.value}</strong>
            </article>
          );
        })}
      </section>

      <section className="kpi-grid secondary-kpis" aria-label="Indicadores operacionais">
        <article className="metric-card">
          <span>Taxa de ocupação</span>
          <strong>{percent(data.taxaOcupacao)}</strong>
        </article>
        <article className="metric-card">
          <span>Ticket médio</span>
          <strong>{money(data.ticketMedio)}</strong>
        </article>
        <article className="metric-card">
          <span>Repasses pendentes</span>
          <strong>{money(data.repassesPendentes)}</strong>
        </article>
        <article className="metric-card">
          <span>Imóveis ativos</span>
          <strong>{data.imoveisAtivos || 0}</strong>
        </article>
      </section>

      <section className="content-grid dashboard-charts">
        <FlowChart items={data.fluxoDiario || []} />
        <OriginChart items={data.reservasPorOrigem || []} />
      </section>

      <section className="content-grid">
        <PerformanceList
          title="Imóveis mais rentáveis"
          badge="Top 5"
          items={data.imoveisMaisRentaveis || []}
          emptyText="Cadastre reservas e receitas para formar o ranking."
        />
        <PerformanceList
          title="Menor desempenho"
          badge="Atenção"
          items={data.imoveisMenorDesempenho || []}
          emptyText="Ainda não há imóveis com resultado no período."
        />
      </section>

      <section className="content-grid">
        <article className="panel">
          <div className="panel-heading">
            <h2>Pendências operacionais</h2>
            <span>Hoje</span>
          </div>
          <div className="status-board">
            <div>
              <small>Limpezas pendentes</small>
              <strong>
                <Sparkles size={16} /> {data.limpezasPendentes || 0}
              </strong>
            </div>
            <div>
              <small>Manutenções pendentes</small>
              <strong>
                <Wrench size={16} /> {data.manutencoesPendentes || 0}
              </strong>
            </div>
            <div>
              <small>Saldo de repasses</small>
              <strong>{money(data.repassesPendentes)}</strong>
            </div>
          </div>
        </article>

        <article className="panel">
          <div className="panel-heading">
            <h2>Leitura rápida</h2>
            <span>Período</span>
          </div>
          <div className="status-board">
            <div>
              <small>Receita menos despesa</small>
              <strong>{money(data.lucroMes)}</strong>
            </div>
            <div>
              <small>Reservas consideradas</small>
              <strong>{data.reservasMes || 0}</strong>
            </div>
            <div>
              <small>Ocupação</small>
              <strong>{percent(data.taxaOcupacao)}</strong>
            </div>
          </div>
        </article>
      </section>
    </div>
  );
}
