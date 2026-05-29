import { ArrowDownRight, ArrowUpRight, CalendarCheck, Home, WalletCards } from 'lucide-react';

const kpis = [
  { label: 'Receita do mês', value: 'R$ 0,00', icon: ArrowUpRight, tone: 'green' },
  { label: 'Despesa do mês', value: 'R$ 0,00', icon: ArrowDownRight, tone: 'red' },
  { label: 'Lucro do mês', value: 'R$ 0,00', icon: WalletCards, tone: 'blue' },
  { label: 'Reservas do mês', value: '0', icon: CalendarCheck, tone: 'yellow' },
];

const nextModules = [
  'Multi-tenant e autenticação',
  'Cadastros de imóveis, proprietários e hóspedes',
  'Reservas com bloqueio de conflito',
  'Calendário operacional',
  'Financeiro e repasses',
];

export function Dashboard() {
  return (
    <div className="dashboard-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Sprint 0</span>
          <h1>Fundação operacional</h1>
          <p>
            Base administrativa pronta para receber autenticação, multi-tenant e os módulos centrais do RentalHub.
          </p>
        </div>
        <button className="primary-action" type="button">
          <Home size={18} />
          Novo imóvel
        </button>
      </section>

      <section className="kpi-grid" aria-label="Indicadores iniciais">
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

      <section className="content-grid">
        <article className="panel">
          <div className="panel-heading">
            <h2>Próximas entregas</h2>
            <span>Roadmap</span>
          </div>
          <div className="timeline-list">
            {nextModules.map((module, index) => (
              <div className="timeline-item" key={module}>
                <span>{index + 1}</span>
                <p>{module}</p>
              </div>
            ))}
          </div>
        </article>

        <article className="panel">
          <div className="panel-heading">
            <h2>Status da operação</h2>
            <span>Base</span>
          </div>
          <div className="status-board">
            <div>
              <small>API</small>
              <strong>http://localhost:5015</strong>
            </div>
            <div>
              <small>Admin</small>
              <strong>http://localhost:5173</strong>
            </div>
            <div>
              <small>PostgreSQL</small>
              <strong>Configurar via environment</strong>
            </div>
          </div>
        </article>
      </section>
    </div>
  );
}
