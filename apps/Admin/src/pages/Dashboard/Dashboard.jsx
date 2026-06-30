import {
  ArrowDownRight,
  BarChart3,
  CalendarCheck,
  Home,
  PieChart,
  RotateCcw,
  WalletCards,
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { dashboardApi } from '@/api/dashboard';
import { EmptyState } from '@/components/EmptyState';

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

function toDateInputValue(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function getCurrentMonthRange() {
  const today = new Date();
  return {
    inicio: toDateInputValue(new Date(today.getFullYear(), today.getMonth(), 1)),
    fim: toDateInputValue(new Date(today.getFullYear(), today.getMonth() + 1, 0)),
  };
}

function FlowChart({ items }) {
  const maxValue = Math.max(
    ...items.map((item) => Math.max(Number(item.receita || 0), Number(item.despesa || 0))),
    1,
  );
  const barHeight = (value) => {
    const numericValue = Number(value || 0);

    return numericValue <= 0 ? 0 : Math.max(6, (numericValue / maxValue) * 100);
  };

  return (
    <article className="panel">
      <div className="panel-heading">
        <h2>Fluxo do período</h2>
        <span>Receita x despesa</span>
      </div>
      {items.length === 0 ? (
        <EmptyState
          compact
          icon={<BarChart3 size={24} />}
          title="Sem movimentações"
          description="Registre receitas e despesas para montar o gráfico."
          actions={[{ label: 'Ir para financeiro', to: '/financeiro', variant: 'secondary' }]}
        />
      ) : (
        <div className="flow-chart" aria-label="Fluxo diário de receitas e despesas">
          {items.map((item) => (
            <div className="flow-day" key={item.data}>
              <div className="flow-bars">
                <span
                  className="income"
                  title={`Receita ${money(item.receita)}`}
                  style={{ height: `${barHeight(item.receita)}%` }}
                />
                <span
                  className="expense"
                  title={`Despesa ${money(item.despesa)}`}
                  style={{ height: `${barHeight(item.despesa)}%` }}
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
        <EmptyState
          compact
          icon={<PieChart size={24} />}
          title="Sem reservas"
          description="As origens aparecem conforme as reservas entram."
          actions={[{ label: 'Criar reserva', to: '/reservas', variant: 'secondary' }]}
        />
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
        <EmptyState compact icon={<Home size={24} />} title="Sem dados no período" description={emptyText} />
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
  const [filters, setFilters] = useState(getCurrentMonthRange);
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

  const lucro = Number(data.lucroMes || 0);
  const receita = Number(data.receitaMes || 0);
  const reservas = Number(data.reservasMes || 0);
  const ocupacao = Number(data.taxaOcupacao || 0);
  const repasses = Number(data.repassesPendentes || 0);
  const limpezas = Number(data.limpezasPendentes || 0);
  const manutencoes = Number(data.manutencoesPendentes || 0);
  const blockers = [repasses > 0, limpezas > 0, manutencoes > 0].filter(Boolean).length;
  const kpis = [
    { label: 'Lucro', value: money(data.lucroMes), icon: WalletCards, tone: lucro < 0 ? 'red' : 'blue', note: `${money(data.receitaMes)} em receitas` },
    { label: 'Reservas', value: String(data.reservasMes || 0), icon: CalendarCheck, tone: 'yellow', note: `${money(data.ticketMedio)} ticket médio` },
    { label: 'Ocupação', value: percent(data.taxaOcupacao), icon: Home, tone: 'green', note: `${data.imoveisAtivos || 0} imóveis ativos` },
    { label: 'Pendências', value: String(blockers), icon: ArrowDownRight, tone: blockers > 0 ? 'red' : 'green', note: 'limpeza, manutenção e repasses' },
  ];
  const healthLabel = blockers > 1 || lucro < 0 ? 'Precisa de atenção' : reservas === 0 ? 'Pronto para começar' : 'Operação saudável';
  const healthTone = blockers > 1 || lucro < 0 ? 'warning' : reservas === 0 ? 'neutral' : 'healthy';
  const alerts = [
    {
      title: reservas === 0 ? 'Nenhuma reserva no período' : `${reservas} reserva(s) no período`,
      description: reservas === 0 ? 'Crie uma reserva para movimentar calendário e financeiro.' : `${percent(ocupacao)} de ocupação.`,
      to: reservas === 0 ? '/reservas' : '/calendario',
      label: reservas === 0 ? 'Criar reserva' : 'Ver calendário',
      active: reservas === 0,
    },
    {
      title: limpezas > 0 ? `${limpezas} limpeza(s) pendente(s)` : 'Limpeza em dia',
      description: limpezas > 0 ? 'Há tarefas aguardando execução ou conclusão.' : 'Sem alerta de limpeza.',
      to: '/limpeza',
      label: 'Abrir',
      active: limpezas > 0,
    },
    {
      title: repasses > 0 ? `${money(repasses)} em repasses` : 'Repasses sem alerta',
      description: repasses > 0 ? 'Valores aguardando pagamento ou baixa.' : 'Nada crítico agora.',
      to: '/repasses',
      label: 'Abrir',
      active: repasses > 0,
    },
    {
      title: manutencoes > 0 ? `${manutencoes} manutenção(ões)` : 'Sem manutenção crítica',
      description: manutencoes > 0 ? 'Ocorrências abertas ou em andamento.' : 'Imóveis sem alerta crítico.',
      to: '/manutencao',
      label: 'Abrir',
      active: manutencoes > 0,
    },
  ];

  return (
    <div className="dashboard-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Dashboard executivo</span>
          <h1>Visão geral</h1>
          <p>Resumo financeiro, ocupação e pendências principais do período.</p>
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
              <small className="metric-note">{kpi.note}</small>
            </article>
          );
        })}
      </section>

      <section className="executive-pulse" aria-label="Central operacional">
        <article className={`operation-health ${healthTone}`}>
          <span className="eyebrow">Leitura rápida</span>
          <h2>{healthLabel}</h2>
          <p>
            {reservas === 0
              ? 'O período ainda não tem reservas. O próximo passo mais importante é criar ou importar uma reserva.'
              : `Receita de ${money(receita)}, lucro de ${money(lucro)} e ${percent(ocupacao)} de ocupação.`}
          </p>
          <div className="health-metrics">
            <span>
              <strong>{money(data.despesaMes)}</strong>
              Despesas
            </span>
            <span>
              <strong>{money(data.ticketMedio)}</strong>
              Ticket médio
            </span>
            <span>
              <strong>{data.imoveisAtivos || 0}</strong>
              Imóveis ativos
            </span>
          </div>
        </article>

        <div className="dashboard-alert-list">
          {alerts.map((alert) => (
            <Link className={`dashboard-alert ${alert.active ? 'active' : ''}`} key={alert.title} to={alert.to}>
              <div>
                <strong>{alert.title}</strong>
                <span>{alert.description}</span>
              </div>
              <small>{alert.label}</small>
            </Link>
          ))}
        </div>
      </section>

      <section className="content-grid dashboard-charts">
        <FlowChart items={data.fluxoDiario || []} />
        <OriginChart items={data.reservasPorOrigem || []} />
      </section>

      <section className="content-grid dashboard-rankings">
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
    </div>
  );
}
