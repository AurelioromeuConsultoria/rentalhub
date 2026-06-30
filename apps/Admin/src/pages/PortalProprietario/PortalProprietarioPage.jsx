import {
  Bath,
  BedDouble,
  Building2,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  CheckCircle2,
  Clock3,
  Download,
  FileText,
  Home,
  Image as ImageIcon,
  KeyRound,
  ReceiptText,
  RotateCcw,
  TrendingUp,
  Users,
  WalletCards,
  X,
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

const reservaStatusOptions = [
  { value: '', label: 'Todos os status' },
  { value: 'Pendente', label: 'Pendente' },
  { value: 'Confirmada', label: 'Confirmada' },
  { value: 'EmAndamento', label: 'Em andamento' },
  { value: 'Finalizada', label: 'Finalizada' },
  { value: 'Cancelada', label: 'Cancelada' },
];

const reservaOrigemOptions = [
  { value: '', label: 'Todas as origens' },
  { value: 'Airbnb', label: 'Airbnb' },
  { value: 'Booking', label: 'Booking' },
  { value: 'Vrbo', label: 'VRBO' },
  { value: 'ReservaDireta', label: 'Reserva direta' },
  { value: 'Outros', label: 'Outros' },
];

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

function parseDate(value) {
  const normalized = dateOnly(value);
  if (!normalized) return null;
  return new Date(`${normalized}T00:00:00`);
}

function periodDays(start, end) {
  const startDate = parseDate(start);
  const endDate = parseDate(end);
  if (!startDate || !endDate || endDate < startDate) return 1;
  return Math.max(1, Math.round((endDate - startDate) / 86400000) + 1);
}

function reservationNights(reservation) {
  const checkIn = parseDate(reservation.checkIn);
  const checkOut = parseDate(reservation.checkOut);
  if (!checkIn || !checkOut || checkOut <= checkIn) return 0;
  return Math.max(1, Math.round((checkOut - checkIn) / 86400000));
}

function clampPercent(value) {
  return Math.max(0, Math.min(100, Math.round(Number(value || 0))));
}

function statusLabel(value) {
  const labels = {
    Ativo: 'Ativo',
    Inativo: 'Inativo',
    EmManutencao: 'Em manutenção',
    Pendente: 'Pendente',
    Confirmada: 'Confirmada',
    EmAndamento: 'Em andamento',
    Finalizada: 'Finalizada',
    Cancelada: 'Cancelada',
    Pago: 'Pago',
    ParcialmentePago: 'Parcial',
  };

  return labels[value] || value || '-';
}

function statusClass(value) {
  if (['Ativo', 'Confirmada', 'Finalizada', 'Pago'].includes(value)) return 'active';
  if (['Pendente', 'EmAndamento', 'ParcialmentePago'].includes(value)) return 'pending';
  return 'inactive';
}

function getErrorMessage(error) {
  return error.response?.data?.message || 'Não foi possível carregar o portal do sócio.';
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

function csvValue(value) {
  const normalized = value == null ? '' : String(value).replace(/\r?\n/g, ' ').trim();
  return `"${normalized.replace(/"/g, '""')}"`;
}

function downloadCsv(fileName, headers, rows) {
  const content = [
    headers.map((header) => csvValue(header.label)).join(';'),
    ...rows.map((row) => headers.map((header) => csvValue(header.value(row))).join(';')),
  ].join('\n');
  const blob = new Blob([`\uFEFF${content}`], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

function monthKeyFromDate(date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  return `${year}-${month}`;
}

function getMonthRange(monthKey) {
  const [year, month] = String(monthKey).split('-').map(Number);
  const start = new Date(year, month - 1, 1);
  const end = new Date(year, month, 0);
  return {
    start: start.toISOString().slice(0, 10),
    end: end.toISOString().slice(0, 10),
  };
}

function shiftMonth(monthKey, offset) {
  const [year, month] = String(monthKey).split('-').map(Number);
  return monthKeyFromDate(new Date(year, month - 1 + offset, 1));
}

function monthLabel(monthKey) {
  const [year, month] = String(monthKey).split('-').map(Number);
  return new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
}

function buildCalendarMonthDays(monthKey) {
  const [year, month] = String(monthKey).split('-').map(Number);
  const monthStart = new Date(year, month - 1, 1);
  const gridStart = new Date(monthStart);
  gridStart.setDate(monthStart.getDate() - monthStart.getDay());

  const days = [];
  const current = new Date(gridStart);
  while (days.length < 42) {
    days.push({
      date: new Date(current),
      inMonth: current.getMonth() === monthStart.getMonth(),
    });
    current.setDate(current.getDate() + 1);
  }

  return days;
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

function StatusBadge({ value }) {
  return <span className={`status-pill ${statusClass(value)}`}>{statusLabel(value)}</span>;
}

function PortalHero({ data, nextReservation, pendingTransfers, periodLabel }) {
  return (
    <section className="portal-owner-hero">
      <div>
        <span className="eyebrow">Portal do sócio</span>
        <h1>{data.proprietarioNome || 'Meus imóveis'}</h1>
        <p>Acompanhe reservas, desempenho dos imóveis, demonstrativos e valores a receber em um só lugar.</p>
      </div>
      <div className="portal-owner-summary">
        <span>Período selecionado</span>
        <strong>{periodLabel}</strong>
        <small>{pendingTransfers > 0 ? `${money(pendingTransfers)} aguardando pagamento` : 'Sem repasses pendentes no período'}</small>
      </div>
      {nextReservation ? (
        <div className="portal-next-card">
          <Clock3 size={18} />
          <span>Próxima reserva</span>
          <strong>{nextReservation.imovelNome}</strong>
          <small>{formatDate(nextReservation.checkIn)} até {formatDate(nextReservation.checkOut)}</small>
        </div>
      ) : (
        <div className="portal-next-card neutral">
          <CheckCircle2 size={18} />
          <span>Agenda</span>
          <strong>Sem próxima reserva</strong>
          <small>Não há reserva futura neste filtro.</small>
        </div>
      )}
    </section>
  );
}

function PortalCalendar({ events, month, onMonthChange, onReservationClick, onTransferClick }) {
  const days = buildCalendarMonthDays(month);
  const visibleEvents = events || [];
  const eventTypes = {
    reserva: 'Reserva',
    repasse: 'Repasse',
    bloqueio: 'Bloqueio',
    manutencao: 'Manutenção',
  };

  return (
    <section className="resource-panel portal-calendar-panel">
      <div className="resource-panel-heading">
        <div>
          <strong>Calendário mensal</strong>
          <small>Reservas, repasses, bloqueios e manutenções vinculados aos seus imóveis.</small>
        </div>
        <div className="portal-calendar-toolbar">
          <button className="icon-button bordered" type="button" aria-label="Mês anterior" onClick={() => onMonthChange(shiftMonth(month, -1))}>
            <ChevronLeft size={17} />
          </button>
          <strong>{monthLabel(month)}</strong>
          <button className="icon-button bordered" type="button" aria-label="Próximo mês" onClick={() => onMonthChange(shiftMonth(month, 1))}>
            <ChevronRight size={17} />
          </button>
        </div>
      </div>
      <div className="portal-calendar-legend">
        {Object.entries(eventTypes).map(([type, label]) => (
          <span key={type}><i className={type} />{label}</span>
        ))}
      </div>
      <div className="portal-calendar-grid" aria-label="Calendário do sócio">
        {['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'].map((day) => (
          <strong className="portal-calendar-weekday" key={day}>{day}</strong>
        ))}
        {days.map((day) => {
          const dayKey = day.date.toISOString().slice(0, 10);
          const dayEvents = visibleEvents
            .filter((event) => {
              const startDate = dateOnly(event.inicio);
              const endDate = dateOnly(event.fim || event.inicio);
              return startDate <= dayKey && endDate >= dayKey;
            })
            .slice(0, 3);
          const eventsCount = visibleEvents.filter((event) => {
            const startDate = dateOnly(event.inicio);
            const endDate = dateOnly(event.fim || event.inicio);
            return startDate <= dayKey && endDate >= dayKey;
          }).length;

          return (
            <div className={`portal-calendar-day ${day.inMonth ? '' : 'muted'}`} key={dayKey}>
              <span>{day.date.getDate()}</span>
              {dayEvents.map((event) => (
                <button
                  className={`portal-calendar-dot ${event.tipo}`}
                  key={`${dayKey}-${event.id}`}
                  type="button"
                  onClick={() => {
                    if (event.tipo === 'reserva') {
                      onReservationClick(Number(String(event.id).replace('reserva-', '')));
                      return;
                    }

                    if (event.tipo === 'repasse') {
                      onTransferClick(Number(String(event.id).replace('repasse-', '')));
                    }
                  }}
                  disabled={!['reserva', 'repasse'].includes(event.tipo)}
                  title={event.imovelNome ? `${event.titulo} · ${event.imovelNome}` : event.titulo}
                >
                  {event.titulo}
                </button>
              ))}
              {eventsCount > 3 && <small className="portal-calendar-more">+{eventsCount - 3} eventos</small>}
            </div>
          );
        })}
      </div>
      <p className="portal-calendar-note">Use as setas para navegar entre meses. O filtro acima acompanha o mês exibido.</p>
    </section>
  );
}

function PropertyDetail({
  property,
  summary,
  reservations,
  movements,
  transfers,
  events,
  onClose,
}) {
  if (!property) return null;

  const nextReservation = reservations
    .filter((reservation) => {
      const checkIn = parseDate(reservation.checkIn);
      return checkIn && checkIn >= new Date(new Date().setHours(0, 0, 0, 0));
    })
    .sort((first, second) => parseDate(first.checkIn) - parseDate(second.checkIn))[0];

  return (
    <section className="resource-panel portal-property-detail">
      <button className="portal-detail-close" type="button" onClick={onClose} aria-label="Fechar detalhe do imóvel">
        <X size={18} />
      </button>
      <div className="portal-property-detail-hero">
        {property.fotoPrincipal ? (
          <img src={property.fotoPrincipal} alt={property.nome} />
        ) : (
          <span><ImageIcon size={32} /></span>
        )}
        <div>
          <small>Detalhe do imóvel</small>
          <h2>{property.nome}</h2>
          <p>{[property.cidade, property.estado].filter(Boolean).join(' / ') || 'Endereço não informado'}</p>
          <StatusBadge value={property.status} />
        </div>
      </div>
      <div className="portal-detail-stats">
        <span><Users size={16} /> {property.quantidadeHospedes} hóspedes</span>
        <span><BedDouble size={16} /> {property.quantidadeQuartos} quartos</span>
        <span><Bath size={16} /> {property.quantidadeBanheiros} banheiros</span>
      </div>
      <div className="portal-detail-grid">
        <article>
          <span>Receitas</span>
          <strong>{money(summary?.receitas)}</strong>
        </article>
        <article>
          <span>Custos</span>
          <strong>{money(summary?.custos)}</strong>
        </article>
        <article>
          <span>Resultado</span>
          <strong>{money(summary?.lucro)}</strong>
        </article>
        <article>
          <span>A receber</span>
          <strong>{money(summary?.repassesPendentes)}</strong>
        </article>
      </div>
      <div className="portal-detail-columns">
        <div>
          <strong>Próxima reserva</strong>
          <p>{nextReservation ? `${nextReservation.hospedeNome} · ${formatDate(nextReservation.checkIn)} até ${formatDate(nextReservation.checkOut)}` : 'Nenhuma reserva futura neste período.'}</p>
        </div>
        <div>
          <strong>Movimentações</strong>
          <p>{movements.length ? `${movements.length} lançamentos no período, totalizando ${money(movements.reduce((sum, item) => sum + Number(item.valor || 0), 0))}.` : 'Sem movimentações no período.'}</p>
        </div>
        <div>
          <strong>Repasses</strong>
          <p>{transfers.length ? `${transfers.length} demonstrativos, ${money(transfers.reduce((sum, item) => sum + Number(item.saldoPendente || 0), 0))} pendente.` : 'Sem repasses no período.'}</p>
        </div>
        <div>
          <strong>Agenda</strong>
          <p>{events.length ? `${events.length} eventos no calendário para este imóvel.` : 'Sem eventos no calendário deste período.'}</p>
        </div>
      </div>
    </section>
  );
}

function ReservationDetail({ reservation, onClose, onDownloadPdf }) {
  if (!reservation) return null;

  const nights = reservationNights(reservation);
  const grossPerNight = nights > 0 ? Number(reservation.receita || 0) / nights : 0;
  const netPerNight = nights > 0 ? Number(reservation.valorLiquido || 0) / nights : 0;

  return (
    <section className="resource-panel portal-reservation-detail">
      <button className="portal-detail-close" type="button" onClick={onClose} aria-label="Fechar detalhe da reserva">
        <X size={18} />
      </button>
      <div className="portal-reservation-detail-heading">
        <CalendarDays size={24} />
        <div>
          <small>Detalhe da reserva</small>
          <h2>{reservation.imovelNome}</h2>
          <p>{reservation.hospedeNome} · {formatDate(reservation.checkIn)} até {formatDate(reservation.checkOut)}</p>
        </div>
        <StatusBadge value={reservation.status} />
      </div>
      <div className="portal-detail-grid">
        <article>
          <span>Origem</span>
          <strong>{reservation.origem}</strong>
        </article>
        <article>
          <span>Diárias</span>
          <strong>{nights}</strong>
        </article>
        <article>
          <span>Bruto por dia</span>
          <strong>{money(grossPerNight)}</strong>
        </article>
        <article>
          <span>Líquido por dia</span>
          <strong>{money(netPerNight)}</strong>
        </article>
        <article>
          <span>Receita bruta</span>
          <strong>{money(reservation.receita)}</strong>
        </article>
        <article>
          <span>Valor líquido</span>
          <strong>{money(reservation.valorLiquido)}</strong>
        </article>
        <article>
          <span>Check-in</span>
          <strong>{formatDate(reservation.checkIn)}</strong>
        </article>
        <article>
          <span>Check-out</span>
          <strong>{formatDate(reservation.checkOut)}</strong>
        </article>
      </div>
      <div className="portal-reservation-actions">
        <button className="primary-action" type="button" onClick={onDownloadPdf}>
          <FileText size={17} />
          Exportar reserva em PDF
        </button>
      </div>
    </section>
  );
}

function TransferDetail({ transfer, detail, loading, onClose, onDownloadPdf }) {
  if (!transfer) return null;

  const current = detail?.id === transfer.id ? detail : transfer;
  const itens = detail?.id === transfer.id ? detail.itens || [] : [];

  return (
    <section className="resource-panel portal-transfer-detail">
      <button className="portal-detail-close" type="button" onClick={onClose} aria-label="Fechar detalhe do repasse">
        <X size={18} />
      </button>
      <div className="portal-reservation-detail-heading">
        <ReceiptText size={24} />
        <div>
          <small>Detalhe do repasse</small>
          <h2>{current.imovelNome || 'Todos os imóveis'}</h2>
          <p>{formatDate(current.periodoInicio)} até {formatDate(current.periodoFim)}</p>
        </div>
        <StatusBadge value={current.status} />
      </div>
      <div className="portal-detail-grid">
        <article>
          <span>Receita reservas</span>
          <strong>{money(current.receitaReservas ?? current.valorRepassar)}</strong>
        </article>
        <article>
          <span>Taxas plataforma</span>
          <strong>{money(current.taxasPlataforma)}</strong>
        </article>
        <article>
          <span>Custos</span>
          <strong>{money(current.custosVinculados)}</strong>
        </article>
        <article>
          <span>Comissão</span>
          <strong>{money(current.comissaoAdministradora)}</strong>
        </article>
        <article>
          <span>Valor a repassar</span>
          <strong>{money(current.valorRepassar)}</strong>
        </article>
        <article>
          <span>Pago</span>
          <strong>{money(current.valorPago)}</strong>
        </article>
        <article>
          <span>Pendente</span>
          <strong>{money(current.saldoPendente)}</strong>
        </article>
        <article>
          <span>Pagamento</span>
          <strong>{current.dataPagamento ? formatDate(current.dataPagamento) : '-'}</strong>
        </article>
      </div>
      {current.observacoes && (
        <div className="portal-detail-note">
          <strong>Observações</strong>
          <p>{current.observacoes}</p>
        </div>
      )}
      <div className="portal-transfer-items">
        <div className="resource-panel-heading compact-heading">
          <div>
            <strong>Composição</strong>
            <small>Itens que formam o demonstrativo.</small>
          </div>
          <span>{loading ? 'Carregando...' : `${itens.length} itens`}</span>
        </div>
        <PortalTable
          columns={[
            { key: 'descricao', label: 'Descrição' },
            { key: 'receita', label: 'Receita', render: (item) => money(item.receita) },
            { key: 'taxas', label: 'Taxas', render: (item) => money(item.taxas) },
            { key: 'custos', label: 'Custos', render: (item) => money(item.custos) },
            { key: 'comissao', label: 'Comissão', render: (item) => money(item.comissao) },
            { key: 'valorLiquido', label: 'Líquido', render: (item) => money(item.valorLiquido) },
          ]}
          emptyText={loading ? 'Carregando composição do repasse.' : 'Este repasse ainda não possui itens detalhados.'}
          items={itens}
        />
      </div>
      <div className="portal-reservation-actions">
        <button className="primary-action" type="button" onClick={onDownloadPdf}>
          <FileText size={17} />
          Baixar demonstrativo
        </button>
      </div>
    </section>
  );
}

function DocumentsPanel({ data, downloadingPdfType, onDownloadPdf }) {
  const documents = [
    {
      key: 'mensal',
      title: 'Demonstrativo mensal',
      description: 'Resumo profissional com desempenho por imóvel, reservas e repasses do período.',
      meta: `${formatDate(data.periodoInicio)} até ${formatDate(data.periodoFim)}`,
    },
    {
      key: 'reservas',
      title: 'Reservas do período',
      description: 'Lista consolidada de reservas com receita bruta e valor líquido.',
      meta: `${data.reservas?.length || 0} reservas`,
    },
    {
      key: 'movimentacoes',
      title: 'Receitas e custos',
      description: 'Movimentações financeiras vinculadas aos imóveis do sócio.',
      meta: `${data.movimentacoes?.length || 0} lançamentos`,
    },
    {
      key: 'repasses',
      title: 'Repasses',
      description: 'Demonstrativos e saldos pendentes para conferência financeira.',
      meta: `${data.repasses?.length || 0} repasses`,
    },
  ];

  return (
    <section className="resource-panel portal-documents-panel">
      <div className="resource-panel-heading">
        <div>
          <strong>Documentos</strong>
          <small>PDFs prontos para conferência, envio ou arquivo.</small>
        </div>
        <span>{documents.length} documentos</span>
      </div>
      <div className="portal-documents-grid">
        {documents.map((document) => (
          <article className="portal-document-card" key={document.key}>
            <FileText size={22} />
            <div>
              <strong>{document.title}</strong>
              <small>{document.description}</small>
              <span>{document.meta}</span>
            </div>
            <button
              className="portal-export-button"
              type="button"
              onClick={() => onDownloadPdf(document.key)}
              disabled={downloadingPdfType === document.key}
            >
              <Download size={16} />
              PDF
            </button>
          </article>
        ))}
      </div>
    </section>
  );
}

export function PortalProprietarioPage() {
  const [filters, setFilters] = useState({
    inicio: currentMonthStart.toISOString().slice(0, 10),
    fim: new Date().toISOString().slice(0, 10),
    imovelId: '',
    reservaStatus: '',
    origem: '',
  });
  const [data, setData] = useState(emptyPortal);
  const [loading, setLoading] = useState(true);
  const [downloadingRepasseId, setDownloadingRepasseId] = useState(null);
  const [downloadingPdfType, setDownloadingPdfType] = useState('');
  const [calendarMonth, setCalendarMonth] = useState(monthKeyFromDate(currentMonthStart));
  const [selectedPropertyId, setSelectedPropertyId] = useState(null);
  const [selectedReservationId, setSelectedReservationId] = useState(null);
  const [selectedTransferId, setSelectedTransferId] = useState(null);
  const [selectedTransferDetail, setSelectedTransferDetail] = useState(null);
  const [error, setError] = useState('');

  const params = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
      imovelId: filters.imovelId || undefined,
      reservaStatus: filters.reservaStatus || undefined,
      origem: filters.origem || undefined,
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

  useEffect(() => {
    if (!selectedTransferId) {
      return undefined;
    }

    let active = true;
    portalProprietarioApi.repasseDetalhe(selectedTransferId)
      .then((response) => {
        if (active) {
          setSelectedTransferDetail(response.data);
        }
      })
      .catch((detailError) => {
        if (active) {
          setError(getErrorMessage(detailError));
          setSelectedTransferDetail(null);
        }
      });

    return () => {
      active = false;
    };
  }, [selectedTransferId]);

  const downloadRepasse = async (repasseId) => {
    if (!repasseId) return;

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

  const downloadReserva = async (reservaId) => {
    if (!reservaId) return;

    setDownloadingPdfType(`reserva-${reservaId}`);
    setError('');
    try {
      const response = await portalProprietarioApi.reservaPdf(reservaId);
      saveBlob(response, `reserva-${reservaId}.pdf`);
    } catch (downloadError) {
      setError(getErrorMessage(downloadError));
    } finally {
      setDownloadingPdfType('');
    }
  };

  const downloadPortalPdf = async (type) => {
    const config = {
      reservas: {
        request: portalProprietarioApi.reservasPdf,
        fileName: 'portal-reservas.pdf',
      },
      movimentacoes: {
        request: portalProprietarioApi.movimentacoesPdf,
        fileName: 'portal-movimentacoes.pdf',
      },
      repasses: {
        request: portalProprietarioApi.repassesPdf,
        fileName: 'portal-repasses.pdf',
      },
      mensal: {
        request: portalProprietarioApi.demonstrativoMensalPdf,
        fileName: 'demonstrativo-mensal.pdf',
      },
    }[type];

    if (!config) return;

    setDownloadingPdfType(type);
    setError('');
    try {
      const response = await config.request(params);
      saveBlob(response, config.fileName);
    } catch (downloadError) {
      setError(getErrorMessage(downloadError));
    } finally {
      setDownloadingPdfType('');
    }
  };

  const exportRows = (fileName, headers, rows, emptyMessage) => {
    if (!rows?.length) {
      setError(emptyMessage);
      return;
    }

    setError('');
    downloadCsv(fileName, headers, rows);
  };

  const saldoOperacional = Number(data.receitas || 0) - Number(data.custos || 0);
  const receitaReservas = (data.reservas || []).reduce((total, reserva) => total + Number(reserva.receita || 0), 0);
  const resultadoEstimado = (data.resumoPorImovel || []).reduce((total, imovel) => total + Number(imovel.lucro || 0), 0);
  const noitesReservadas = (data.reservas || []).reduce((total, reserva) => total + reservationNights(reserva), 0);
  const ocupacaoEstimada = clampPercent((noitesReservadas / Math.max(1, periodDays(filters.inicio, filters.fim) * Math.max(1, data.totalImoveis || 1))) * 100);
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  const nextReservation = (data.reservas || [])
    .filter((reserva) => {
      const checkIn = parseDate(reserva.checkIn);
      return checkIn && checkIn >= hoje;
    })
    .sort((first, second) => parseDate(first.checkIn) - parseDate(second.checkIn))[0];
  const periodLabel = `${formatDate(filters.inicio)} - ${formatDate(filters.fim)}`;
  const selectedProperty = useMemo(
    () => (data.imoveis || []).find((property) => property.id === selectedPropertyId),
    [data.imoveis, selectedPropertyId],
  );
  const selectedPropertySummary = useMemo(
    () => (data.resumoPorImovel || []).find((property) => property.imovelId === selectedPropertyId),
    [data.resumoPorImovel, selectedPropertyId],
  );
  const selectedPropertyReservations = useMemo(
    () => (data.reservas || []).filter((reservation) => reservation.imovelId === selectedPropertyId),
    [data.reservas, selectedPropertyId],
  );
  const selectedPropertyMovements = useMemo(
    () => (data.movimentacoes || []).filter((movement) => movement.imovelId === selectedPropertyId),
    [data.movimentacoes, selectedPropertyId],
  );
  const selectedPropertyTransfers = useMemo(
    () => (data.repasses || []).filter((transfer) => transfer.imovelId === selectedPropertyId),
    [data.repasses, selectedPropertyId],
  );
  const selectedPropertyEvents = useMemo(
    () => (data.calendario || []).filter((event) => event.imovelId === selectedPropertyId),
    [data.calendario, selectedPropertyId],
  );
  const selectedReservation = useMemo(
    () => (data.reservas || []).find((reservation) => reservation.id === selectedReservationId),
    [data.reservas, selectedReservationId],
  );
  const selectedTransfer = useMemo(
    () => (data.repasses || []).find((transfer) => transfer.id === selectedTransferId),
    [data.repasses, selectedTransferId],
  );
  const loadingTransferDetail = Boolean(selectedTransferId && selectedTransferDetail?.id !== selectedTransferId);
  const handleCalendarMonthChange = (nextMonth) => {
    const range = getMonthRange(nextMonth);
    setCalendarMonth(nextMonth);
    setFilters((current) => ({
      ...current,
      inicio: range.start,
      fim: range.end,
    }));
  };

  return (
    <div className="resource-page">
      <PortalHero
        data={data}
        nextReservation={nextReservation}
        pendingTransfers={data.repassesPendentes}
        periodLabel={periodLabel}
      />

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
        <label className="form-field">
          <span>Status da reserva</span>
          <select value={filters.reservaStatus} onChange={(event) => setFilters((current) => ({ ...current, reservaStatus: event.target.value }))}>
            {reservaStatusOptions.map((option) => (
              <option key={option.value || 'todos'} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <label className="form-field">
          <span>Origem</span>
          <select value={filters.origem} onChange={(event) => setFilters((current) => ({ ...current, origem: event.target.value }))}>
            {reservaOrigemOptions.map((option) => (
              <option key={option.value || 'todas'} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <div className="portal-filter-action">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
        </div>
      </section>

      {error && <div className="form-alert">{error}</div>}
      {loading && <div className="loading-line">Carregando portal...</div>}

      <section className="kpi-grid portal-kpi-grid">
        <article className="metric-card" title="Soma dos saldos pendentes de repasses no período selecionado.">
          <div className="metric-icon green">
            <WalletCards size={19} />
          </div>
          <span>Valor a receber</span>
          <strong>{money(data.repassesPendentes)}</strong>
          <small className="metric-note">Saldo pendente de repasses.</small>
        </article>
        <article className="metric-card" title="Receitas menos custos vinculados aos imóveis no período.">
          <div className="metric-icon blue">
            <TrendingUp size={19} />
          </div>
          <span>Resultado estimado</span>
          <strong>{money(resultadoEstimado || saldoOperacional)}</strong>
          <small className="metric-note">Receitas menos custos.</small>
        </article>
        <article className="metric-card" title="Soma do valor bruto das reservas mais taxa de limpeza.">
          <div className="metric-icon yellow">
            <ReceiptText size={19} />
          </div>
          <span>Receita de reservas</span>
          <strong>{money(receitaReservas)}</strong>
          <small className="metric-note">Hospedagem + limpeza.</small>
        </article>
        <article className="metric-card" title="Estimativa baseada nas diárias reservadas dividido pelo total de dias disponíveis no filtro.">
          <div className="metric-icon blue">
            <CalendarDays size={19} />
          </div>
          <span>Ocupação estimada</span>
          <strong>{ocupacaoEstimada}%</strong>
          <small className="metric-note">Diárias reservadas no filtro.</small>
        </article>
        <article className="metric-card" title="Quantidade de imóveis vinculados ao sócio.">
          <div className="metric-icon blue">
            <Building2 size={19} />
          </div>
          <span>Imóveis</span>
          <strong>{data.totalImoveis}</strong>
          <small className="metric-note">Unidades vinculadas.</small>
        </article>
        <article className="metric-card" title="Reservas encontradas dentro do período selecionado.">
          <div className="metric-icon green">
            <KeyRound size={19} />
          </div>
          <span>Reservas</span>
          <strong>{data.totalReservas}</strong>
          <small className="metric-note">Reservas no período.</small>
        </article>
      </section>

      <PortalCalendar
        events={data.calendario}
        month={calendarMonth}
        onMonthChange={handleCalendarMonthChange}
        onReservationClick={setSelectedReservationId}
        onTransferClick={setSelectedTransferId}
      />

      <DocumentsPanel
        data={data}
        downloadingPdfType={downloadingPdfType}
        onDownloadPdf={downloadPortalPdf}
      />

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
                <div className="portal-property-progress">
                  <span style={{ width: `${clampPercent((Number(item.reservas || 0) / Math.max(1, data.totalReservas || 1)) * 100)}%` }} />
                </div>
                <button className="portal-property-action" type="button" onClick={() => setSelectedPropertyId(item.imovelId)}>
                  Ver detalhe do imóvel
                </button>
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

      <PropertyDetail
        property={selectedProperty}
        summary={selectedPropertySummary}
        reservations={selectedPropertyReservations}
        movements={selectedPropertyMovements}
        transfers={selectedPropertyTransfers}
        events={selectedPropertyEvents}
        onClose={() => setSelectedPropertyId(null)}
      />

      <ReservationDetail
        reservation={selectedReservation}
        onClose={() => setSelectedReservationId(null)}
        onDownloadPdf={() => downloadReserva(selectedReservation?.id)}
      />

      <TransferDetail
        transfer={selectedTransfer}
        detail={selectedTransferDetail}
        loading={loadingTransferDetail}
        onClose={() => {
          setSelectedTransferId(null);
          setSelectedTransferDetail(null);
        }}
        onDownloadPdf={() => downloadRepasse(selectedTransfer?.id)}
      />

      <section className="content-grid">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Meus imóveis</strong>
              <small>Unidades vinculadas ao sócio.</small>
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
              { key: 'status', label: 'Status', render: (item) => <StatusBadge value={item.status} /> },
            ]}
            emptyText="Nenhum imóvel vinculado ao seu usuário."
            items={data.imoveis}
          />
        </article>

        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Calendário</strong>
              <small>Próximos eventos destacados do período.</small>
            </div>
            <span>{data.calendario?.length || 0} eventos</span>
          </div>
          <div className="timeline-list">
            {(data.calendario || []).slice(0, 8).map((event) => (
              <div className={`timeline-item portal-event ${event.tipo}`} key={event.id}>
                <span>{formatDate(event.inicio).slice(0, 2)}</span>
                <p>
                  <strong>{event.titulo}</strong>
                  <small>{statusLabel(event.status)} · {formatDate(event.inicio)}{dateOnly(event.fim) !== dateOnly(event.inicio) ? ` até ${formatDate(event.fim)}` : ''}</small>
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
          <div className="portal-heading-actions">
            <span>{data.reservas?.length || 0} reservas</span>
            <button
              className="portal-export-button"
              type="button"
              aria-label="Exportar reservas"
              onClick={() => exportRows(
                'portal-reservas.csv',
                [
                  { label: 'Check-in', value: (item) => formatDate(item.checkIn) },
                  { label: 'Check-out', value: (item) => formatDate(item.checkOut) },
                  { label: 'Imóvel', value: (item) => item.imovelNome },
                  { label: 'Hóspede', value: (item) => item.hospedeNome },
                  { label: 'Origem', value: (item) => item.origem },
                  { label: 'Receita', value: (item) => money(item.receita) },
                  { label: 'Líquido', value: (item) => money(item.valorLiquido) },
                  { label: 'Status', value: (item) => statusLabel(item.status) },
                ],
                data.reservas,
                'Não há reservas para exportar neste período.',
              )}
            >
              <Download size={16} />
              CSV
            </button>
            <button
              className="portal-export-button"
              type="button"
              aria-label="Exportar reservas em PDF"
              onClick={() => downloadPortalPdf('reservas')}
              disabled={downloadingPdfType === 'reservas'}
            >
              <FileText size={16} />
              PDF
            </button>
          </div>
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
            { key: 'status', label: 'Status', render: (item) => <StatusBadge value={item.status} /> },
            {
              key: 'acoes',
              label: '',
              render: (item) => (
                <button className="portal-link-button" type="button" onClick={() => setSelectedReservationId(item.id)}>
                  Detalhe
                </button>
              ),
            },
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
            <div className="portal-heading-actions">
              <span>{data.movimentacoes?.length || 0} lançamentos</span>
              <button
                className="portal-export-button"
                type="button"
                aria-label="Exportar movimentações"
                onClick={() => exportRows(
                  'portal-movimentacoes.csv',
                  [
                    { label: 'Data', value: (item) => formatDate(item.data) },
                    { label: 'Tipo', value: (item) => item.tipo },
                    { label: 'Categoria', value: (item) => item.categoriaNome },
                    { label: 'Imóvel', value: (item) => item.imovelNome || '-' },
                    { label: 'Descrição', value: (item) => item.descricao },
                    { label: 'Valor', value: (item) => money(item.valor) },
                  ],
                  data.movimentacoes,
                  'Não há movimentações para exportar neste período.',
                )}
              >
                <Download size={16} />
                CSV
              </button>
              <button
                className="portal-export-button"
                type="button"
                aria-label="Exportar movimentações em PDF"
                onClick={() => downloadPortalPdf('movimentacoes')}
                disabled={downloadingPdfType === 'movimentacoes'}
              >
                <FileText size={16} />
                PDF
              </button>
            </div>
          </div>
          <PortalTable
            columns={[
              { key: 'data', label: 'Data', render: (item) => formatDate(item.data) },
              {
                key: 'tipo',
                label: 'Tipo',
                render: (item) => <span className={`status-pill ${item.tipo === 'Receita' ? 'active' : 'pending'}`}>{item.tipo}</span>,
              },
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
            <div className="portal-heading-actions">
              <span>{data.repasses?.length || 0} repasses</span>
              <button
                className="portal-export-button"
                type="button"
                aria-label="Exportar repasses"
                onClick={() => exportRows(
                  'portal-repasses.csv',
                  [
                    { label: 'Período', value: (item) => `${formatDate(item.periodoInicio)} - ${formatDate(item.periodoFim)}` },
                    { label: 'Imóvel', value: (item) => item.imovelNome || 'Todos' },
                    { label: 'Valor', value: (item) => money(item.valorRepassar) },
                    { label: 'Pago', value: (item) => money(item.valorPago) },
                    { label: 'Pendente', value: (item) => money(item.saldoPendente) },
                    { label: 'Status', value: (item) => statusLabel(item.status) },
                  ],
                  data.repasses,
                  'Não há repasses para exportar neste período.',
                )}
              >
                <Download size={16} />
                CSV
              </button>
              <button
                className="portal-export-button"
                type="button"
                aria-label="Exportar repasses em PDF"
                onClick={() => downloadPortalPdf('repasses')}
                disabled={downloadingPdfType === 'repasses'}
              >
                <FileText size={16} />
                PDF
              </button>
            </div>
          </div>
          <PortalTable
            columns={[
              { key: 'periodoFim', label: 'Período', render: (item) => `${formatDate(item.periodoInicio)} - ${formatDate(item.periodoFim)}` },
              { key: 'imovelNome', label: 'Imóvel', render: (item) => item.imovelNome || 'Todos' },
              { key: 'valorRepassar', label: 'Valor', render: (item) => money(item.valorRepassar) },
              { key: 'valorPago', label: 'Pago', render: (item) => money(item.valorPago) },
              { key: 'saldoPendente', label: 'Pendente', render: (item) => money(item.saldoPendente) },
              { key: 'status', label: 'Status', render: (item) => <StatusBadge value={item.status} /> },
              {
                key: 'acoes',
                label: '',
                render: (item) => (
                  <div className="table-action-row">
                    <button className="portal-link-button" type="button" onClick={() => setSelectedTransferId(item.id)}>
                      Detalhe
                    </button>
                    <button
                      className="icon-button bordered"
                      type="button"
                      aria-label="Baixar demonstrativo"
                      onClick={() => downloadRepasse(item.id)}
                      disabled={downloadingRepasseId === item.id}
                    >
                      <Download size={16} />
                    </button>
                  </div>
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
