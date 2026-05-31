import { CalendarDays, ChevronLeft, ChevronRight, Plus, RotateCcw, Save, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { calendarioApi } from '@/api/calendario';
import { imoveisApi } from '@/api/cadastros';

const weekDays = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];

const tipoOptions = [
  { value: 1, label: 'Bloqueio' },
  { value: 2, label: 'Manutenção' },
];

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

function monthLabel(date) {
  return new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' }).format(date);
}

function getMonthRange(monthDate) {
  const firstDay = new Date(monthDate.getFullYear(), monthDate.getMonth(), 1);
  const lastDay = new Date(monthDate.getFullYear(), monthDate.getMonth() + 1, 0);
  const gridStart = new Date(firstDay);
  gridStart.setDate(firstDay.getDate() - firstDay.getDay());
  const gridEnd = new Date(lastDay);
  gridEnd.setDate(lastDay.getDate() + (6 - lastDay.getDay()) + 1);

  return { firstDay, lastDay, gridStart, gridEnd };
}

function buildDays(monthDate) {
  const { firstDay, gridStart } = getMonthRange(monthDate);
  return Array.from({ length: 42 }, (_, index) => {
    const day = new Date(gridStart);
    day.setDate(gridStart.getDate() + index);
    return {
      date: day,
      key: toInputDate(day),
      isCurrentMonth: day.getMonth() === firstDay.getMonth(),
      isToday: toInputDate(day) === toInputDate(new Date()),
    };
  });
}

function eventTouchesDay(event, dayKey) {
  const day = parseDateOnly(dayKey);
  const start = parseDateOnly(event.inicio);
  const end = parseDateOnly(event.fim);
  return start <= day && day < end;
}

function getErrorMessage(error) {
  return error.response?.data?.message || 'Não foi possível concluir a operação.';
}

export function CalendarioPage() {
  const [monthDate, setMonthDate] = useState(() => new Date());
  const [events, setEvents] = useState([]);
  const [imoveis, setImoveis] = useState([]);
  const [selectedImovelId, setSelectedImovelId] = useState('');
  const [form, setForm] = useState(emptyBlock);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const days = useMemo(() => buildDays(monthDate), [monthDate]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');

    const { gridStart, gridEnd } = getMonthRange(monthDate);

    try {
      const [eventsResponse, imoveisResponse] = await Promise.all([
        calendarioApi.events({
          inicio: toInputDate(gridStart),
          fim: toInputDate(gridEnd),
          imovelId: selectedImovelId || undefined,
        }),
        imoveisApi.list({ status: 1, pageSize: 100 }),
      ]);

      setEvents(eventsResponse.data || []);
      setImoveis(imoveisResponse.data?.items || []);
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [monthDate, selectedImovelId]);

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

  const goToPreviousMonth = () => {
    setMonthDate((current) => new Date(current.getFullYear(), current.getMonth() - 1, 1));
  };

  const goToNextMonth = () => {
    setMonthDate((current) => new Date(current.getFullYear(), current.getMonth() + 1, 1));
  };

  const saveBlock = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');

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
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const deleteBlock = async (event) => {
    setError('');
    try {
      await calendarioApi.deleteBlock(event.entityId);
      await load();
    } catch (deleteError) {
      setError(getErrorMessage(deleteError));
    }
  };

  return (
    <div className="calendar-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Operação</span>
          <h1>Calendário</h1>
          <p>Visão mensal de reservas, check-ins, check-outs, bloqueios e períodos de manutenção.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Mês anterior" onClick={goToPreviousMonth}>
            <ChevronLeft size={18} />
          </button>
          <button className="icon-button bordered" type="button" aria-label="Próximo mês" onClick={goToNextMonth}>
            <ChevronRight size={18} />
          </button>
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
        </div>
      </section>

      <section className="calendar-layout">
        <article className="calendar-panel">
          <div className="calendar-toolbar">
            <strong>{monthLabel(monthDate)}</strong>
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

          {error && <div className="form-alert">{error}</div>}

          <div className="calendar-grid">
            {weekDays.map((day) => (
              <div className="calendar-weekday" key={day}>
                {day}
              </div>
            ))}

            {days.map((day) => {
              const dayEvents = events.filter((event) => eventTouchesDay(event, day.key));
              return (
                <div
                  className={`calendar-day${day.isCurrentMonth ? '' : ' muted'}${day.isToday ? ' today' : ''}`}
                  key={day.key}
                >
                  <div className="calendar-day-header">
                    <strong>{day.date.getDate()}</strong>
                    {dayEvents.some((event) => event.tipo === 'reserva' && event.inicio.slice(0, 10) === day.key) && <span>Check-in</span>}
                    {dayEvents.some((event) => event.tipo === 'reserva' && event.fim.slice(0, 10) === day.key) && <span>Check-out</span>}
                  </div>

                  {loading ? (
                    <small className="calendar-loading">...</small>
                  ) : (
                    <div className="calendar-events">
                      {dayEvents.slice(0, 4).map((event) => (
                        <div className={`calendar-event ${event.tipo}`} key={`${day.key}-${event.id}`}>
                          <div>
                            <strong>{event.titulo}</strong>
                            <span>{event.imovelNome}</span>
                            {event.descricao && <small>{event.descricao}</small>}
                          </div>
                          {event.id.startsWith('bloqueio-') && event.inicio.slice(0, 10) === day.key && (
                            <button type="button" aria-label="Remover bloqueio" onClick={() => deleteBlock(event)}>
                              <Trash2 size={13} />
                            </button>
                          )}
                        </div>
                      ))}
                      {dayEvents.length > 4 && <span className="calendar-more">+{dayEvents.length - 4}</span>}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </article>

        <form className="resource-form" onSubmit={saveBlock}>
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

          <div className="calendar-legend">
            <span className="reserva">Reserva</span>
            <span className="bloqueio">Bloqueio</span>
            <span className="limpeza">Limpeza</span>
            <span className="manutencao">Manutenção</span>
          </div>
        </form>
      </section>
    </div>
  );
}
