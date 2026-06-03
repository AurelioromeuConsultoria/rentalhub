import {
  Bath,
  BedDouble,
  Building2,
  CalendarDays,
  Download,
  Home,
  Image as ImageIcon,
  ReceiptText,
  RotateCcw,
  TrendingUp,
  Users,
  WalletCards,
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { portalProprietarioApi } from '@/api/portalProprietario';

const currentMonthStart = new Date();
currentMonthStart.setDate(1);

const emptyPortal = {
  proprietarioNome: '',
  imovelSelecionadoId: null,
  totalImoveis: 0,
  totalReservas: 0,
  receitas: 0,
  custos: 0,
  repassesGerados: 0,
  repassesPendentes: 0,
  imoveis: [],
  reservas: [],
  movimentacoes: [],
  repasses: [],
  calendario: [],
  resumoPorImovel: [],
};

function money(value) {
  return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(Number(value || 0));
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

function getErrorMessage(error) {
  return error.response?.data?.message || 'Não foi possível carregar o portal do proprietário.';
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

function PortalTable({ columns, items, emptyText }) {
  if (!items || items.length === 0) {
    return (
      <div className="inline-empty compact">
        <Home size={24} />
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
            <tr key={item.id || index}>
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

export function PortalProprietarioPage() {
  const [filters, setFilters] = useState({
    inicio: currentMonthStart.toISOString().slice(0, 10),
    fim: new Date().toISOString().slice(0, 10),
    imovelId: '',
  });
  const [data, setData] = useState(emptyPortal);
  const [loading, setLoading] = useState(true);
  const [downloadingRepasseId, setDownloadingRepasseId] = useState(null);
  const [error, setError] = useState('');

  const params = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
      imovelId: filters.imovelId || undefined,
    }),
    [filters],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const response = await portalProprietarioApi.get(params);
      setData(response.data || emptyPortal);
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

  const downloadRepasse = async (repasseId) => {
    setDownloadingRepasseId(repasseId);
    setError('');
    try {
      const response = await portalProprietarioApi.demonstrativoRepassePdf(repasseId);
      saveBlob(response, `demonstrativo-repasse-${repasseId}.pdf`);
    } catch (downloadError) {
      setError(getErrorMessage(downloadError));
    } finally {
      setDownloadingRepasseId(null);
    }
  };

  const saldoOperacional = Number(data.receitas || 0) - Number(data.custos || 0);
  const repassesPagos = Number(data.repassesGerados || 0) - Number(data.repassesPendentes || 0);

  return (
    <div className="resource-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Portal do proprietário</span>
          <h1>{data.proprietarioNome || 'Meus imóveis'}</h1>
          <p>Resumo de imóveis, calendário, reservas, receitas, custos, repasses e demonstrativos.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
        </div>
      </section>

      <section className="resource-panel dashboard-filters portal-filters">
        <label className="form-field">
          <span>Início</span>
          <input type="date" value={filters.inicio} onChange={(event) => setFilters((current) => ({ ...current, inicio: event.target.value }))} />
        </label>
        <label className="form-field">
          <span>Fim</span>
          <input type="date" value={filters.fim} onChange={(event) => setFilters((current) => ({ ...current, fim: event.target.value }))} />
        </label>
        <label className="form-field">
          <span>Imóvel</span>
          <select value={filters.imovelId} onChange={(event) => setFilters((current) => ({ ...current, imovelId: event.target.value }))}>
            <option value="">Todos os imóveis</option>
            {(data.imoveis || []).map((imovel) => (
              <option key={imovel.id} value={imovel.id}>{imovel.nome}</option>
            ))}
          </select>
        </label>
      </section>

      {error && <div className="form-alert">{error}</div>}
      {loading && <div className="loading-line">Carregando portal...</div>}

      <section className="kpi-grid">
        <article className="metric-card">
          <div className="metric-icon blue">
            <Building2 size={19} />
          </div>
          <span>Imóveis</span>
          <strong>{data.totalImoveis}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon yellow">
            <CalendarDays size={19} />
          </div>
          <span>Reservas</span>
          <strong>{data.totalReservas}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon green">
            <WalletCards size={19} />
          </div>
          <span>Receitas</span>
          <strong>{money(data.receitas)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon red">
            <ReceiptText size={19} />
          </div>
          <span>Repasses pendentes</span>
          <strong>{money(data.repassesPendentes)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon blue">
            <TrendingUp size={19} />
          </div>
          <span>Saldo operacional</span>
          <strong>{money(saldoOperacional)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon green">
            <WalletCards size={19} />
          </div>
          <span>Repasses pagos</span>
          <strong>{money(repassesPagos)}</strong>
        </article>
      </section>

      <section className="resource-panel">
        <div className="resource-panel-heading">
          <div>
            <strong>Desempenho por imóvel</strong>
            <small>Receita, custos, lucro e pendências consolidados no período.</small>
          </div>
          <span>{data.resumoPorImovel?.length || 0} imóveis</span>
        </div>
        {data.resumoPorImovel?.length ? (
          <div className="portal-property-grid">
            {data.resumoPorImovel.map((item) => (
              <article className="portal-property-card" key={item.imovelId}>
                {item.fotoPrincipal ? (
                  <img src={item.fotoPrincipal} alt={item.imovelNome} />
                ) : (
                  <span className="portal-property-placeholder"><ImageIcon size={24} /></span>
                )}
                <div>
                  <strong>{item.imovelNome}</strong>
                  <small>{item.reservas} reservas no período</small>
                </div>
                <dl>
                  <div>
                    <dt>Receitas</dt>
                    <dd>{money(item.receitas)}</dd>
                  </div>
                  <div>
                    <dt>Custos</dt>
                    <dd>{money(item.custos)}</dd>
                  </div>
                  <div>
                    <dt>Lucro</dt>
                    <dd className={Number(item.lucro || 0) >= 0 ? 'positive' : 'negative'}>{money(item.lucro)}</dd>
                  </div>
                  <div>
                    <dt>Pendente</dt>
                    <dd>{money(item.repassesPendentes)}</dd>
                  </div>
                </dl>
              </article>
            ))}
          </div>
        ) : (
          <div className="inline-empty compact">
            <TrendingUp size={24} />
            <strong>Sem desempenho no período</strong>
            <span>Selecione outro período ou cadastre reservas para formar o resumo por imóvel.</span>
          </div>
        )}
      </section>

      <section className="content-grid">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Meus imóveis</strong>
              <small>Unidades vinculadas ao proprietário.</small>
            </div>
            <span>{data.imoveis?.length || 0} imóveis</span>
          </div>
          <PortalTable
            columns={[
              {
                key: 'nome',
                label: 'Imóvel',
                render: (item) => (
                  <div className="property-cell">
                    {item.fotoPrincipal ? (
                      <img src={item.fotoPrincipal} alt={item.nome} />
                    ) : (
                      <span><ImageIcon size={17} /></span>
                    )}
                    <div>
                      <strong>{item.nome}</strong>
                      <small>{item.codigoInterno}</small>
                    </div>
                  </div>
                ),
              },
              { key: 'cidade', label: 'Cidade', render: (item) => [item.cidade, item.estado].filter(Boolean).join(' / ') || '-' },
              {
                key: 'capacidade',
                label: 'Capacidade',
                render: (item) => (
                  <span className="portal-capacity">
                    <Users size={14} /> {item.quantidadeHospedes}
                    <BedDouble size={14} /> {item.quantidadeQuartos}
                    <Bath size={14} /> {item.quantidadeBanheiros}
                  </span>
                ),
              },
              { key: 'status', label: 'Status' },
            ]}
            emptyText="Nenhum imóvel vinculado ao seu usuário."
            items={data.imoveis}
          />
        </article>

        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Calendário</strong>
              <small>Reservas e repasses do período.</small>
            </div>
            <span>{data.calendario?.length || 0} eventos</span>
          </div>
          <div className="timeline-list">
            {(data.calendario || []).slice(0, 8).map((event) => (
              <div className="timeline-item" key={event.id}>
                <span>{formatDate(event.inicio).slice(0, 2)}</span>
                <p>
                  {event.titulo} · {event.tipo} · {event.status} · {formatDate(event.inicio)}{dateOnly(event.fim) !== dateOnly(event.inicio) ? ` até ${formatDate(event.fim)}` : ''}
                </p>
              </div>
            ))}
            {(data.calendario || []).length === 0 && (
              <div className="inline-empty compact">
                <CalendarDays size={24} />
                <strong>Sem eventos</strong>
                <span>Não há reservas ou repasses no período.</span>
              </div>
            )}
          </div>
        </article>
      </section>

      <section className="resource-panel">
        <div className="resource-panel-heading">
          <div>
            <strong>Reservas</strong>
            <small>Reservas dos seus imóveis no período.</small>
          </div>
          <span>{data.reservas?.length || 0} reservas</span>
        </div>
        <PortalTable
          columns={[
            { key: 'checkIn', label: 'Check-in', render: (item) => formatDate(item.checkIn) },
            { key: 'checkOut', label: 'Check-out', render: (item) => formatDate(item.checkOut) },
            { key: 'imovelNome', label: 'Imóvel' },
            { key: 'hospedeNome', label: 'Hóspede' },
            { key: 'origem', label: 'Origem' },
            { key: 'receita', label: 'Receita', render: (item) => money(item.receita) },
            { key: 'valorLiquido', label: 'Líquido', render: (item) => money(item.valorLiquido) },
            { key: 'status', label: 'Status' },
          ]}
          emptyText="Não há reservas para seus imóveis no período."
          items={data.reservas}
        />
      </section>

      <section className="content-grid">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Receitas e custos</strong>
              <small>Movimentações vinculadas aos seus imóveis.</small>
            </div>
            <span>{data.movimentacoes?.length || 0} lançamentos</span>
          </div>
          <PortalTable
            columns={[
              { key: 'data', label: 'Data', render: (item) => formatDate(item.data) },
              { key: 'tipo', label: 'Tipo' },
              { key: 'categoriaNome', label: 'Categoria' },
              { key: 'imovelNome', label: 'Imóvel', render: (item) => item.imovelNome || '-' },
              { key: 'descricao', label: 'Descrição' },
              { key: 'valor', label: 'Valor', render: (item) => money(item.valor) },
            ]}
            emptyText="Não há movimentações vinculadas no período."
            items={data.movimentacoes}
          />
        </article>

        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Repasses</strong>
              <small>Demonstrativos e saldos pendentes.</small>
            </div>
            <span>{data.repasses?.length || 0} repasses</span>
          </div>
          <PortalTable
            columns={[
              { key: 'periodoFim', label: 'Período', render: (item) => `${formatDate(item.periodoInicio)} - ${formatDate(item.periodoFim)}` },
              { key: 'imovelNome', label: 'Imóvel', render: (item) => item.imovelNome || 'Todos' },
              { key: 'valorRepassar', label: 'Valor', render: (item) => money(item.valorRepassar) },
              { key: 'valorPago', label: 'Pago', render: (item) => money(item.valorPago) },
              { key: 'saldoPendente', label: 'Pendente', render: (item) => money(item.saldoPendente) },
              { key: 'status', label: 'Status' },
              {
                key: 'acoes',
                label: '',
                render: (item) => (
                  <button
                    className="icon-button bordered"
                    type="button"
                    aria-label="Baixar demonstrativo"
                    onClick={() => downloadRepasse(item.id)}
                    disabled={downloadingRepasseId === item.id}
                  >
                    <Download size={16} />
                  </button>
                ),
              },
            ]}
            emptyText="Não há repasses gerados no período."
            items={data.repasses}
          />
        </article>
      </section>
    </div>
  );
}
