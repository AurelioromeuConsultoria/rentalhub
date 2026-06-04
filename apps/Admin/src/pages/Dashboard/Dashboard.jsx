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
import { Link } from 'react-router-dom';
import { dashboardApi } from '@/api/dashboard';
import { EmptyState } from '@/components/EmptyState';

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
  const lucro = Number(data.lucroMes || 0);
  const receita = Number(data.receitaMes || 0);
  const reservas = Number(data.reservasMes || 0);
  const ocupacao = Number(data.taxaOcupacao || 0);
  const repasses = Number(data.repassesPendentes || 0);
  const limpezas = Number(data.limpezasPendentes || 0);
  const manutencoes = Number(data.manutencoesPendentes || 0);
  const blockers = [repasses > 0, limpezas > 0, manutencoes > 0].filter(Boolean).length;
  const healthLabel = blockers > 1 || lucro < 0 ? 'Atenção operacional' : reservas === 0 ? 'Primeira operação' : 'Operação sob controle';
  const healthTone = blockers > 1 || lucro < 0 ? 'warning' : reservas === 0 ? 'neutral' : 'healthy';
  const nextActions = [
    {
      title: reservas === 0 ? 'Cadastre a primeira reserva' : 'Acompanhe a agenda',
      description: reservas === 0 ? 'Transforme um imóvel cadastrado em operação real.' : 'Veja disponibilidade, check-ins, check-outs e bloqueios.',
      to: reservas === 0 ? '/reservas' : '/calendario',
      label: reservas === 0 ? 'Nova reserva' : 'Abrir calendário',
      tone: reservas === 0 ? 'primary' : 'blue',
    },
    {
      title: limpezas > 0 ? 'Limpezas pendentes' : 'Limpeza em dia',
      description: limpezas > 0 ? `${limpezas} tarefa(s) precisam de execução ou conclusão.` : 'Mantenha a agenda pronta para próximos check-ins.',
      to: '/limpeza',
      label: 'Ver limpeza',
      tone: limpezas > 0 ? 'warning' : 'green',
    },
    {
      title: repasses > 0 ? 'Repasses em aberto' : 'Repasses sem alerta',
      description: repasses > 0 ? `${money(repasses)} aguardando pagamento ou baixa.` : 'Gere demonstrativos quando fechar um período.',
      to: '/repasses',
      label: 'Ver repasses',
      tone: repasses > 0 ? 'warning' : 'green',
    },
    {
      title: manutencoes > 0 ? 'Manutenção pendente' : 'Imóveis operacionais',
      description: manutencoes > 0 ? `${manutencoes} ocorrência(s) abertas ou em andamento.` : 'Sem ocorrência crítica no período selecionado.',
      to: '/manutencao',
      label: 'Ver manutenção',
      tone: manutencoes > 0 ? 'danger' : 'green',
    },
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

      <section className="executive-pulse" aria-label="Central operacional">
        <article className={`operation-health ${healthTone}`}>
          <span className="eyebrow">Central operacional</span>
          <h2>{healthLabel}</h2>
          <p>
            {reservas === 0
              ? 'Comece pela reserva: ela movimenta calendário, financeiro, limpeza e repasses.'
              : `No período, você tem ${reservas} reserva(s), ${percent(ocupacao)} de ocupação e ${money(receita)} em receita.`}
          </p>
          <div className="health-metrics">
            <span>
              <strong>{money(lucro)}</strong>
              Lucro
            </span>
            <span>
              <strong>{blockers}</strong>
              Alertas
            </span>
            <span>
              <strong>{percent(ocupacao)}</strong>
              Ocupação
            </span>
          </div>
        </article>

        <div className="next-action-grid">
          {nextActions.map((action) => (
            <Link className={`next-action-card ${action.tone}`} key={action.title} to={action.to}>
              <strong>{action.title}</strong>
              <span>{action.description}</span>
              <small>{action.label}</small>
            </Link>
          ))}
        </div>
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
