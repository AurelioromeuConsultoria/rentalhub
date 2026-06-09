import {
  AlertTriangle,
  CalendarCheck,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  Clock,
  DoorOpen,
  Hammer,
  Lock,
  Plus,
  RotateCcw,
  Save,
  Sparkles,
  Trash2,
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { calendarioApi } from '@/api/calendario';
import { imoveisApi } from '@/api/cadastros';
import { EmptyState } from '@/components/EmptyState';
import { confirmAction, getFriendlyErrorMessage } from '@/lib/uiFeedback';

const dayFormatter = new Intl.DateTimeFormat('pt-BR', { weekday: 'short' });
const shortDateFormatter = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit' });
const longDateFormatter = new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' });

const tipoOptions = [
  { value: 1, label: 'Bloqueio' },
  { value: 2, label: 'Manutenção' },
];

const viewModes = [
  { value: 'month', label: 'Mês' },
  { value: 'week', label: 'Semana' },
  { value: 'day', label: 'Dia' },
];

const weekDays = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

const emptyBlock = {
  imovelId: '',
  inicio: '',
  fim: '',
  tipo: 1,
  motivo: '',
};

function toInputDate(date) {
  return date.toISOString().slice(0, 10);
}

function parseDateOnly(value) {
  const [year, month, day] = String(value).slice(0, 10).split('-').map(Number);
  return new Date(year, month - 1, day);
}

function addDays(date, amount) {
  const next = new Date(date);
  next.setDate(next.getDate() + amount);
  return next;
}

function differenceInDays(start, end) {
  const startDate = parseDateOnly(toInputDate(start));
  const endDate = parseDateOnly(toInputDate(end));
  return Math.round((endDate - startDate) / 86400000);
}

function formatDate(value) {
  if (!value) return '-';
  if (value instanceof Date) return longDateFormatter.format(value);
  return longDateFormatter.format(parseDateOnly(value));
}

function monthLabel(date) {
  const label = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' }).format(date);
  return label.charAt(0).toUpperCase() + label.slice(1);
}

function getVisibleRange(anchorDate, viewMode) {
  if (viewMode === 'day') {
    const start = parseDateOnly(toInputDate(anchorDate));
    return { start, end: addDays(start, 1) };
  }

  if (viewMode === 'week') {
    const start = parseDateOnly(toInputDate(anchorDate));
    start.setDate(start.getDate() - start.getDay());
    return { start, end: addDays(start, 7) };
  }

  const start = new Date(anchorDate.getFullYear(), anchorDate.getMonth(), 1);
  const end = new Date(anchorDate.getFullYear(), anchorDate.getMonth() + 1, 1);
  return { start, end };
}

function buildDays(anchorDate, viewMode) {
  const { start, end } = getVisibleRange(anchorDate, viewMode);
  const total = differenceInDays(start, end);

  return Array.from({ length: total }, (_, index) => {
    const date = addDays(start, index);
    const key = toInputDate(date);
    return {
      date,
      key,
      dayNumber: date.getDate(),
      weekday: dayFormatter.format(date).replace('.', ''),
      label: shortDateFormatter.format(date),
      isToday: key === toInputDate(new Date()),
      isWeekend: date.getDay() === 0 || date.getDay() === 6,
    };
  });
}

function buildCalendarCells(anchorDate, viewMode) {
  if (viewMode === 'day') {
    const date = parseDateOnly(toInputDate(anchorDate));
    return [{
      date,
      key: toInputDate(date),
      dayNumber: date.getDate(),
      inRange: true,
      isToday: toInputDate(date) === toInputDate(new Date()),
      isWeekend: date.getDay() === 0 || date.getDay() === 6,
    }];
  }

  if (viewMode === 'week') {
    const { start } = getVisibleRange(anchorDate, viewMode);
    return Array.from({ length: 7 }, (_, index) => {
      const date = addDays(start, index);
      return {
        date,
        key: toInputDate(date),
        dayNumber: date.getDate(),
        inRange: true,
        isToday: toInputDate(date) === toInputDate(new Date()),
        isWeekend: date.getDay() === 0 || date.getDay() === 6,
      };
    });
  }

  const firstDay = new Date(anchorDate.getFullYear(), anchorDate.getMonth(), 1);
  const totalMonthDays = new Date(anchorDate.getFullYear(), anchorDate.getMonth() + 1, 0).getDate();
  const leadingDays = firstDay.getDay();
  const rows = Math.ceil((leadingDays + totalMonthDays) / 7);
  const gridStart = addDays(firstDay, -leadingDays);

  return Array.from({ length: rows * 7 }, (_, index) => {
    const date = addDays(gridStart, index);
    return {
      date,
      key: toInputDate(date),
      dayNumber: date.getDate(),
      inRange: date.getMonth() === anchorDate.getMonth(),
      isToday: toInputDate(date) === toInputDate(new Date()),
      isWeekend: date.getDay() === 0 || date.getDay() === 6,
    };
  });
}

function eventTouchesRange(event, startKey, endKey) {
  const start = parseDateOnly(startKey);
  const end = parseDateOnly(endKey);
  const eventStart = parseDateOnly(event.inicio);
  const eventEnd = parseDateOnly(event.fim);
  return eventEnd > start && eventStart < end;
}

function eventTouchesDay(event, dayKey) {
  const day = parseDateOnly(dayKey);
  const start = parseDateOnly(event.inicio);
  const end = parseDateOnly(event.fim);
  return start <= day && day < end;
}

function eventStartsOn(event, dayKey) {
  return String(event.inicio).slice(0, 10) === dayKey;
}

function eventEndsOn(event, dayKey) {
  return String(event.fim).slice(0, 10) === dayKey;
}

function getEventMeta(event) {
  if (event.tipo === 'reserva') {
    if (event.status === 1) return { label: 'Pendente', icon: Clock };
    if (event.status === 2) return { label: 'Confirmada', icon: CalendarCheck };
    if (event.status === 3) return { label: 'Em andamento', icon: DoorOpen };
    if (event.status === 4) return { label: 'Finalizada', icon: ClipboardCheck };
    return { label: 'Reserva', icon: CalendarDays };
  }

  if (event.tipo === 'limpeza') return { label: 'Limpeza', icon: Sparkles };
  if (event.tipo === 'manutencao') return { label: 'Manutenção', icon: Hammer };
  return { label: 'Bloqueio', icon: Lock };
}

function getEventText(event) {
  const meta = getEventMeta(event);
  if (event.tipo === 'reserva') {
    return `${meta.label}${event.descricao ? ` · ${event.descricao}` : ''}`;
  }

  return event.titulo || meta.label;
}

function getPeriodTitle(viewMode, anchorDate, visibleStart, visibleEnd) {
  if (viewMode === 'month') return monthLabel(anchorDate);
  if (viewMode === 'day') return formatDate(visibleStart);
  return `${formatDate(visibleStart)} a ${formatDate(addDays(parseDateOnly(visibleEnd), -1))}`;
}

function getErrorMessage(error) {
  return getFriendlyErrorMessage(error);
}

function getSelectionMessage(selection, conflicts) {
  if (!selection.imovelId || !selection.inicio || !selection.fim) {
    return {
      tone: 'neutral',
      title: 'Selecione um período',
      description: 'Clique em um dia para definir o check-in. Clique em outro dia do mesmo imóvel para definir o check-out.',
    };
  }

  if (conflicts.length > 0) {
    return {
      tone: 'danger',
      title: 'Período indisponível',
      description: 'Existe reserva, bloqueio, limpeza ou manutenção nesse intervalo.',
    };
  }

  return {
    tone: 'success',
    title: 'Período livre',
    description: 'Você pode criar uma reserva ou bloquear esse intervalo para o imóvel selecionado.',
  };
}

export function CalendarioPage() {
  const navigate = useNavigate();
  const [anchorDate, setAnchorDate] = useState(() => new Date());
  const [viewMode, setViewMode] = useState('month');
  const [events, setEvents] = useState([]);
  const [imoveis, setImoveis] = useState([]);
  const [selectedImovelId, setSelectedImovelId] = useState('');
  const [selectedDay, setSelectedDay] = useState(() => toInputDate(new Date()));
  const [selection, setSelection] = useState({ imovelId: '', inicio: '', fim: '' });
  const [form, setForm] = useState(emptyBlock);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const days = useMemo(() => buildDays(anchorDate, viewMode), [anchorDate, viewMode]);
  const calendarCells = useMemo(() => buildCalendarCells(anchorDate, viewMode), [anchorDate, viewMode]);
  const visibleStart = days[0]?.key || toInputDate(anchorDate);
  const visibleEnd = days.length > 0 ? toInputDate(addDays(parseDateOnly(days.at(-1).key), 1)) : toInputDate(addDays(anchorDate, 1));

  const visibleImoveis = useMemo(() => {
    if (!selectedImovelId) return imoveis;
    return imoveis.filter((imovel) => String(imovel.id) === String(selectedImovelId));
  }, [imoveis, selectedImovelId]);

  const selectedImovel = useMemo(
    () => imoveis.find((imovel) => String(imovel.id) === String(selection.imovelId)),
    [imoveis, selection.imovelId],
  );

  const selectedConflicts = useMemo(() => {
    if (!selection.imovelId || !selection.inicio || !selection.fim) return [];
    return events.filter(
      (event) =>
        String(event.imovelId) === String(selection.imovelId) &&
        eventTouchesRange(event, selection.inicio, selection.fim),
    );
  }, [events, selection]);

  const selectionStatus = useMemo(() => getSelectionMessage(selection, selectedConflicts), [selection, selectedConflicts]);

  const daySummary = useMemo(() => {
    const todayKey = toInputDate(new Date());
    const relevantEvents = events.filter((event) => eventTouchesDay(event, todayKey));
    const checkIns = events.filter((event) => event.tipo === 'reserva' && eventStartsOn(event, todayKey)).length;
    const checkOuts = events.filter((event) => event.tipo === 'reserva' && eventEndsOn(event, todayKey)).length;
    const limpezas = relevantEvents.filter((event) => event.tipo === 'limpeza').length;
    const manutencoes = relevantEvents.filter((event) => event.tipo === 'manutencao').length;

    return { checkIns, checkOuts, limpezas, manutencoes };
  }, [events]);

  const availableProperties = useMemo(() => {
    if (!selection.inicio || !selection.fim) return [];
    return imoveis.filter((imovel) => {
      return !events.some(
        (event) =>
          event.imovelId === imovel.id &&
          eventTouchesRange(event, selection.inicio, selection.fim),
      );
    });
  }, [events, imoveis, selection.fim, selection.inicio]);

  const selectedDayEvents = useMemo(
    () => events.filter((event) => eventTouchesDay(event, selectedDay)),
    [events, selectedDay],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError('');

    try {
      const [eventsResponse, imoveisResponse] = await Promise.all([
        calendarioApi.events({
          inicio: visibleStart,
          fim: visibleEnd,
          imovelId: selectedImovelId || undefined,
        }),
        imoveisApi.list({ status: 1, pageSize: 200 }),
      ]);

      setEvents(eventsResponse.data || []);
      setImoveis(imoveisResponse.data?.items || []);
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [selectedImovelId, visibleEnd, visibleStart]);

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, [load]);

  useEffect(() => {
    if (!form.imovelId && imoveis.length > 0) {
      const timeout = setTimeout(() => {
        setForm((current) => ({ ...current, imovelId: String(imoveis[0].id) }));
      }, 0);
      return () => clearTimeout(timeout);
    }
    return undefined;
  }, [form.imovelId, imoveis]);

  const goToPreviousRange = () => {
    setAnchorDate((current) => {
      if (viewMode === 'day') return addDays(current, -1);
      if (viewMode === 'week') return addDays(current, -7);
      return new Date(current.getFullYear(), current.getMonth() - 1, 1);
    });
  };

  const goToNextRange = () => {
    setAnchorDate((current) => {
      if (viewMode === 'day') return addDays(current, 1);
      if (viewMode === 'week') return addDays(current, 7);
      return new Date(current.getFullYear(), current.getMonth() + 1, 1);
    });
  };

  const goToToday = () => {
    setAnchorDate(new Date());
    setSelectedDay(toInputDate(new Date()));
  };

  const changeViewMode = (mode) => {
    setViewMode(mode);
    if (mode !== 'month' && selectedDay) {
      setAnchorDate(parseDateOnly(selectedDay));
    }
  };

  const selectDay = (imovelId, dayKey) => {
    setError('');
    setSuccess('');
    setSelection((current) => {
      const sameProperty = String(current.imovelId) === String(imovelId);
      const shouldStartOver = !sameProperty || !current.inicio || current.fim || dayKey <= current.inicio;
      const next = shouldStartOver
        ? { imovelId: String(imovelId), inicio: dayKey, fim: '' }
        : { imovelId: String(imovelId), inicio: current.inicio, fim: toInputDate(addDays(parseDateOnly(dayKey), 1)) };

      setForm((formState) => ({
        ...formState,
        imovelId: String(imovelId),
        inicio: next.inicio,
        fim: next.fim || toInputDate(addDays(parseDateOnly(next.inicio), 1)),
      }));

      return next;
    });
  };

  const selectCalendarDay = (dayKey) => {
    setSelectedDay(dayKey);

    const fallbackImovelId = selectedImovelId || form.imovelId || visibleImoveis[0]?.id;
    if (fallbackImovelId) {
      selectDay(fallbackImovelId, dayKey);
    }

    if (viewMode !== 'day') {
      setAnchorDate(parseDateOnly(dayKey));
      setViewMode('day');
    }
  };

  const fillBlockFromSelection = () => {
    if (!selection.imovelId || !selection.inicio) return;
    setForm((current) => ({
      ...current,
      imovelId: selection.imovelId,
      inicio: selection.inicio,
      fim: selection.fim || toInputDate(addDays(parseDateOnly(selection.inicio), 1)),
    }));
  };

  const createReservaFromSelection = () => {
    if (!selection.imovelId || !selection.inicio || !selection.fim || selectedConflicts.length > 0) return;

    const params = new URLSearchParams({
      imovelId: selection.imovelId,
      checkIn: selection.inicio,
      checkOut: selection.fim,
    });

    navigate(`/reservas?${params.toString()}`);
  };

  const saveBlock = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');

    const payload = {
      imovelId: Number(form.imovelId),
      inicio: form.inicio,
      fim: form.fim,
      tipo: Number(form.tipo),
      motivo: form.motivo.trim(),
    };

    try {
      await calendarioApi.createBlock(payload);
      setForm((current) => ({
        ...emptyBlock,
        imovelId: current.imovelId,
      }));
      setSelection({ imovelId: '', inicio: '', fim: '' });
      setSuccess('Bloqueio salvo no calendário.');
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const deleteBlock = async (event) => {
    const confirmed = confirmAction(
      'Remover este bloqueio?',
      `${event.titulo} em ${event.imovelNome} será removido do calendário.`,
    );

    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');
    try {
      await calendarioApi.deleteBlock(event.entityId);
      setSuccess('Bloqueio removido do calendário.');
      await load();
    } catch (deleteError) {
      setError(getErrorMessage(deleteError));
    }
  };

  return (
    <div className="calendar-page">
      <section className="page-heading calendar-heading">
        <div>
          <span className="eyebrow">Operação</span>
          <h1>Calendário</h1>
          <p>Mapa de ocupação por imóvel com reservas, bloqueios, limpezas, manutenções e criação rápida de agenda.</p>
        </div>
        <div className="resource-actions">
          <button className="secondary-action compact" type="button" onClick={goToToday}>
            Hoje
          </button>
          <button className="icon-button bordered" type="button" aria-label="Período anterior" onClick={goToPreviousRange}>
            <ChevronLeft size={18} />
          </button>
          <button className="icon-button bordered" type="button" aria-label="Próximo período" onClick={goToNextRange}>
            <ChevronRight size={18} />
          </button>
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
        </div>
      </section>

      <section className="calendar-kpis">
        <article>
          <DoorOpen size={20} />
          <span>Check-ins hoje</span>
          <strong>{daySummary.checkIns}</strong>
        </article>
        <article>
          <CalendarCheck size={20} />
          <span>Check-outs hoje</span>
          <strong>{daySummary.checkOuts}</strong>
        </article>
        <article>
          <Sparkles size={20} />
          <span>Limpezas hoje</span>
          <strong>{daySummary.limpezas}</strong>
        </article>
        <article>
          <Hammer size={20} />
          <span>Manutenções hoje</span>
          <strong>{daySummary.manutencoes}</strong>
        </article>
      </section>

      <section className="calendar-layout strong">
        <article className="calendar-panel calendar-board-panel">
          <div className="calendar-toolbar strong">
            <div>
              <strong>{getPeriodTitle(viewMode, anchorDate, visibleStart, visibleEnd)}</strong>
              <span>{visibleImoveis.length} imóveis · {events.length} eventos no período</span>
            </div>

            <div className="calendar-toolbar-controls">
              <div className="segmented-control" aria-label="Visualização do calendário">
                {viewModes.map((mode) => (
                  <button
                    className={viewMode === mode.value ? 'active' : ''}
                    key={mode.value}
                    type="button"
                    onClick={() => changeViewMode(mode.value)}
                  >
                    {mode.label}
                  </button>
                ))}
              </div>

              <label>
                <span>Imóvel</span>
                <select value={selectedImovelId} onChange={(event) => setSelectedImovelId(event.target.value)}>
                  <option value="">Todos</option>
                  {imoveis.map((imovel) => (
                    <option key={imovel.id} value={imovel.id}>
                      {imovel.nome}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          </div>

          {error && <div className="form-alert">{error}</div>}
          {success && <div className="form-success">{success}</div>}

          {loading ? (
            <div className="loading-line">Carregando agenda operacional...</div>
          ) : visibleImoveis.length === 0 ? (
            <EmptyState
              icon={CalendarDays}
              title="Nenhum imóvel ativo encontrado"
              description="Cadastre ou ative um imóvel para montar a agenda operacional."
            />
          ) : (
            <div className={`calendar-readable-board ${viewMode}`}>
              {viewMode === 'month' && (
                <div className="calendar-weekdays">
                  {weekDays.map((day) => (
                    <span key={day}>{day}</span>
                  ))}
                </div>
              )}

              <div className="calendar-month-grid">
                {calendarCells.map((day) => {
                  const dayEvents = events.filter((event) => eventTouchesDay(event, day.key));
                  const visibleDayEvents = viewMode === 'month' ? dayEvents.slice(0, 3) : dayEvents;
                  const selected =
                    selection.inicio &&
                    parseDateOnly(selection.inicio) <= parseDateOnly(day.key) &&
                    parseDateOnly(day.key) < parseDateOnly(selection.fim || toInputDate(addDays(parseDateOnly(selection.inicio), 1)));

                  return (
                    <button
                      className={`calendar-day-card${day.inRange ? '' : ' muted'}${day.isToday ? ' today' : ''}${day.isWeekend ? ' weekend' : ''}${selected ? ' selected' : ''}`}
                      data-calendar-day={day.key}
                      key={day.key}
                      type="button"
                      onClick={() => selectCalendarDay(day.key)}
                    >
                      <div className="calendar-day-card-header">
                        <strong>{day.dayNumber}</strong>
                        {viewMode !== 'month' && <span>{dayFormatter.format(day.date).replace('.', '')}</span>}
                      </div>

                      <div className="calendar-cell-events">
                        {visibleDayEvents.map((event) => {
                          const meta = getEventMeta(event);
                          const Icon = meta.icon;
                          return (
                            <span className={`calendar-cell-event ${event.tipo}`} key={event.id} title={`${event.imovelNome} · ${getEventText(event)}`}>
                              {viewMode !== 'month' && <Icon size={15} />}
                              <span>
                                <strong>{getEventText(event)}</strong>
                                {viewMode !== 'month' && <em>{event.imovelNome}</em>}
                              </span>
                            </span>
                          );
                        })}
                        {viewMode === 'month' && dayEvents.length > visibleDayEvents.length && (
                          <span className="calendar-cell-more">+{dayEvents.length - visibleDayEvents.length} eventos</span>
                        )}
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          )}
        </article>

        <aside className="calendar-side-panel">
          <article className="calendar-day-agenda">
            <div>
              <strong>Detalhe do dia</strong>
              <span>{formatDate(selectedDay)} · {selectedDayEvents.length} eventos</span>
            </div>

            {selectedDayEvents.length > 0 ? (
              <div className="calendar-day-agenda-list">
                {selectedDayEvents.map((event) => {
                  const meta = getEventMeta(event);
                  const Icon = meta.icon;
                  return (
                    <article className={`calendar-agenda-event ${event.tipo}`} key={event.id}>
                      <Icon size={16} />
                      <div>
                        <strong>{getEventText(event)}</strong>
                        <span>{event.imovelNome} · {formatDate(event.inicio)} a {formatDate(event.fim)}</span>
                      </div>
                      {event.id.startsWith('bloqueio-') && (
                        <button type="button" aria-label="Remover bloqueio" onClick={() => deleteBlock(event)}>
                          <Trash2 size={14} />
                        </button>
                      )}
                    </article>
                  );
                })}
              </div>
            ) : (
              <p>Dia livre no período filtrado. Clique em outro dia ou selecione um intervalo para criar reserva/bloqueio.</p>
            )}
          </article>

          <article className={`calendar-selection-card ${selectionStatus.tone}`}>
            <div className="form-title">
              {selectionStatus.tone === 'danger' ? <AlertTriangle size={18} /> : <CalendarCheck size={18} />}
              <strong>Disponibilidade</strong>
            </div>
            <h2>{selectionStatus.title}</h2>
            <p>{selectionStatus.description}</p>

            <dl className="calendar-selection-details">
              <div>
                <dt>Imóvel</dt>
                <dd>{selectedImovel?.nome || 'Não selecionado'}</dd>
              </div>
              <div>
                <dt>Check-in</dt>
                <dd>{formatDate(selection.inicio)}</dd>
              </div>
              <div>
                <dt>Check-out</dt>
                <dd>{formatDate(selection.fim)}</dd>
              </div>
              <div>
                <dt>Diárias</dt>
                <dd>{selection.inicio && selection.fim ? differenceInDays(parseDateOnly(selection.inicio), parseDateOnly(selection.fim)) : 0}</dd>
              </div>
            </dl>

            {selectedConflicts.length > 0 && (
              <div className="calendar-conflict-list">
                <strong>Conflitos no período</strong>
                {selectedConflicts.slice(0, 4).map((event) => (
                  <span key={event.id}>{getEventText(event)} · {formatDate(event.inicio)} a {formatDate(event.fim)}</span>
                ))}
              </div>
            )}

            <div className="calendar-selection-actions">
              <button
                className="primary-action full"
                type="button"
                disabled={!selection.imovelId || !selection.inicio || !selection.fim || selectedConflicts.length > 0}
                onClick={createReservaFromSelection}
              >
                <Plus size={18} />
                Criar reserva
              </button>
              <button
                className="secondary-action full"
                type="button"
                disabled={!selection.imovelId || !selection.inicio}
                onClick={fillBlockFromSelection}
              >
                Usar no bloqueio
              </button>
            </div>
          </article>

          <form className="resource-form calendar-block-form" onSubmit={saveBlock}>
            <div className="form-title">
              <Plus size={18} />
              <strong>Novo bloqueio</strong>
            </div>
            <div className="form-grid">
              <label className="form-field span-2">
                <span>Imóvel</span>
                <select
                  value={form.imovelId}
                  onChange={(event) => setForm((current) => ({ ...current, imovelId: event.target.value }))}
                  required
                >
                  <option value="">Selecione</option>
                  {imoveis.map((imovel) => (
                    <option key={imovel.id} value={imovel.id}>
                      {imovel.nome}
                    </option>
                  ))}
                </select>
              </label>
              <label className="form-field">
                <span>Início</span>
                <input
                  type="date"
                  value={form.inicio}
                  onChange={(event) => setForm((current) => ({ ...current, inicio: event.target.value }))}
                  required
                />
              </label>
              <label className="form-field">
                <span>Fim</span>
                <input
                  type="date"
                  value={form.fim}
                  onChange={(event) => setForm((current) => ({ ...current, fim: event.target.value }))}
                  required
                />
              </label>
              <label className="form-field span-2">
                <span>Tipo</span>
                <select value={form.tipo} onChange={(event) => setForm((current) => ({ ...current, tipo: Number(event.target.value) }))}>
                  {tipoOptions.map((option) => (
                    <option key={option.value} value={option.value}>
                      {option.label}
                    </option>
                  ))}
                </select>
              </label>
              <label className="form-field span-2">
                <span>Motivo</span>
                <textarea
                  value={form.motivo}
                  onChange={(event) => setForm((current) => ({ ...current, motivo: event.target.value }))}
                  placeholder="Ex.: manutenção preventiva, bloqueio do proprietário"
                  required
                />
              </label>
            </div>
            <button className="primary-action full" type="submit" disabled={saving || imoveis.length === 0}>
              <Save size={18} />
              {saving ? 'Salvando...' : 'Salvar bloqueio'}
            </button>
          </form>

          <article className="calendar-availability-list">
            <strong>Imóveis livres no período</strong>
            {selection.inicio && selection.fim ? (
              availableProperties.length > 0 ? (
                <div>
                  {availableProperties.slice(0, 8).map((imovel) => (
                    <button
                      key={imovel.id}
                      type="button"
                      onClick={() => setSelection((current) => ({ ...current, imovelId: String(imovel.id) }))}
                    >
                      {imovel.nome}
                      <span>{imovel.quantidadeHospedes || 0} hóspedes</span>
                    </button>
                  ))}
                </div>
              ) : (
                <p>Nenhum imóvel livre nesse intervalo.</p>
              )
            ) : (
              <p>Selecione check-in e check-out para consultar disponibilidade.</p>
            )}
          </article>

          <div className="calendar-legend">
            <span className="reserva">Reserva</span>
            <span className="bloqueio">Bloqueio</span>
            <span className="limpeza">Limpeza</span>
            <span className="manutencao">Manutenção</span>
          </div>
        </aside>
      </section>
    </div>
  );
}
