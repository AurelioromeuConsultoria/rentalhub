import { CalendarDays, Edit3, Plus, RotateCcw, Save, Search, XCircle } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { configuracoesApi } from '@/api/administracao';
import { calendarioApi } from '@/api/calendario';
import { hospedesApi, imoveisApi } from '@/api/cadastros';
import { reservasApi } from '@/api/reservas';
import { EmptyState } from '@/components/EmptyState';
import { MoneyField } from '@/components/Form/MoneyField';
import { confirmAction, getFriendlyErrorMessage } from '@/lib/uiFeedback';

const origemOptions = [
  { value: 1, label: 'Airbnb' },
  { value: 2, label: 'Booking' },
  { value: 3, label: 'VRBO' },
  { value: 4, label: 'Reserva Direta' },
  { value: 5, label: 'Outros' },
];

const statusOptions = [
  { value: 1, label: 'Pendente' },
  { value: 2, label: 'Confirmada' },
  { value: 3, label: 'Em andamento' },
  { value: 4, label: 'Finalizada' },
  { value: 5, label: 'Cancelada' },
];

const emptyReserva = {
  imovelId: '',
  hospedeId: '',
  origem: 4,
  checkIn: '',
  checkOut: '',
  numeroHospedes: 1,
  valorHospedagem: 0,
  taxaLimpeza: 0,
  taxaPlataforma: 0,
  comissaoAdministradora: 0,
  status: 1,
  observacoes: '',
};

const initialAvailability = {
  status: 'idle',
  message: 'Escolha imóvel, check-in e check-out para verificar a agenda.',
  events: [],
};

function extractItems(response) {
  return response.data?.items || [];
}

function getErrorMessage(error) {
  return getFriendlyErrorMessage(error);
}

function buildEmptyReserva(settings = null) {
  return {
    ...emptyReserva,
    taxaLimpeza: Number(settings?.taxaLimpezaPadrao || 0),
    comissaoAdministradora: Number(settings?.comissaoPadraoAdministradora || 0),
  };
}

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

function labelFor(options, value) {
  return options.find((option) => option.value === Number(value))?.label || '-';
}

function scrollToForm() {
  document.getElementById('reserva-form')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function calculateValorLiquido(form) {
  return (
    Number(form.valorHospedagem || 0) +
    Number(form.taxaLimpeza || 0) -
    Number(form.taxaPlataforma || 0) -
    Number(form.comissaoAdministradora || 0)
  );
}

function calculateTotalReserva(form) {
  return Number(form.valorHospedagem || 0) + Number(form.taxaLimpeza || 0);
}

function calculateDiarias(form) {
  const checkIn = dateOnly(form.checkIn);
  const checkOut = dateOnly(form.checkOut);
  if (!checkIn || !checkOut) {
    return 0;
  }

  const [inYear, inMonth, inDay] = checkIn.split('-').map(Number);
  const [outYear, outMonth, outDay] = checkOut.split('-').map(Number);
  const start = Date.UTC(inYear, inMonth - 1, inDay);
  const end = Date.UTC(outYear, outMonth - 1, outDay);
  const days = Math.round((end - start) / 86400000);

  return Math.max(days, 0);
}

function calculateValorPorDia(value, form) {
  const diarias = calculateDiarias(form);
  return diarias > 0 ? Number(value || 0) / diarias : 0;
}

function getAvailabilityInputError(form) {
  if (!form.imovelId || !form.checkIn || !form.checkOut) {
    return 'Escolha imóvel, check-in e check-out para verificar a agenda.';
  }

  if (calculateDiarias(form) <= 0) {
    return 'O check-out precisa ser posterior ao check-in.';
  }

  return '';
}

function eventTypeLabel(type) {
  const labels = {
    reserva: 'Reserva',
    bloqueio: 'Bloqueio',
    limpeza: 'Limpeza',
    manutencao: 'Manutenção',
  };

  return labels[type] || 'Evento';
}

function isBlockingEvent(event) {
  return ['reserva', 'bloqueio', 'manutencao'].includes(event.tipo);
}

function formatEventPeriod(event) {
  return `${formatDate(event.inicio)} até ${formatDate(event.fim)}`;
}

function StatusPill({ status }) {
  const isCanceled = Number(status) === 5;
  return (
    <span className={`status-pill ${isCanceled ? 'inactive' : 'active'}`}>
      {labelFor(statusOptions, status)}
    </span>
  );
}

function TextField({ label, value, onChange, required, type = 'text', min, step }) {
  return (
    <label className="form-field">
      <span>{label}</span>
      <input
        type={type}
        value={value ?? ''}
        min={min}
        step={step}
        onChange={(event) => onChange(event.target.value)}
        required={required}
      />
    </label>
  );
}

function TextAreaField({ label, value, onChange }) {
  return (
    <label className="form-field span-2">
      <span>{label}</span>
      <textarea value={value ?? ''} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function SelectField({ label, value, onChange, children, required }) {
  return (
    <label className="form-field">
      <span>{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)} required={required}>
        {children}
      </select>
    </label>
  );
}

export function ReservasPage() {
  const [reservas, setReservas] = useState([]);
  const [imoveis, setImoveis] = useState([]);
  const [hospedes, setHospedes] = useState([]);
  const [tenantSettings, setTenantSettings] = useState(null);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState(buildEmptyReserva());
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [availability, setAvailability] = useState(initialAvailability);
  const selectedImovelId = form.imovelId;
  const selectedCheckIn = form.checkIn;
  const selectedCheckOut = form.checkOut;

  const filteredReservas = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    if (!normalizedSearch) return reservas;

    return reservas.filter((reserva) =>
      [reserva.imovelNome, reserva.hospedeNome, labelFor(origemOptions, reserva.origem), labelFor(statusOptions, reserva.status)]
        .filter(Boolean)
        .some((value) => value.toLowerCase().includes(normalizedSearch)),
    );
  }, [reservas, search]);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [reservasResponse, imoveisResponse, hospedesResponse, configuracoesResponse] = await Promise.all([
        reservasApi.list(),
        imoveisApi.list({ status: 1, pageSize: 100 }),
        hospedesApi.list({ ativo: true, pageSize: 100 }),
        configuracoesApi.get(),
      ]);
      setReservas(extractItems(reservasResponse));
      setImoveis(extractItems(imoveisResponse));
      setHospedes(extractItems(hospedesResponse));
      setTenantSettings(configuracoesResponse.data?.tenant || null);
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, []);

  useEffect(() => {
    if (editingId || !tenantSettings) {
      return;
    }

    const timeout = setTimeout(() => {
      setForm((current) => ({
        ...buildEmptyReserva(tenantSettings),
        imovelId: current.imovelId,
        hospedeId: current.hospedeId,
        origem: current.origem,
        checkIn: current.checkIn,
        checkOut: current.checkOut,
        numeroHospedes: current.numeroHospedes,
        valorHospedagem: current.valorHospedagem,
        taxaPlataforma: current.taxaPlataforma,
        status: current.status,
        observacoes: current.observacoes,
      }));
    }, 0);

    return () => clearTimeout(timeout);
  }, [editingId, tenantSettings]);

  useEffect(() => {
    let active = true;
    const timeout = setTimeout(async () => {
      const periodForm = {
        imovelId: selectedImovelId,
        checkIn: selectedCheckIn,
        checkOut: selectedCheckOut,
      };
      const inputError = getAvailabilityInputError(periodForm);
      if (inputError) {
        if (active) {
          setAvailability({
            status: 'idle',
            message: inputError,
            events: [],
          });
        }
        return;
      }

      if (active) {
        setAvailability({
          status: 'loading',
          message: 'Verificando agenda do imóvel...',
          events: [],
        });
      }

      try {
        const [availabilityResponse, eventsResponse] = await Promise.all([
          reservasApi.availability({
            imovelId: selectedImovelId,
            checkIn: selectedCheckIn,
            checkOut: selectedCheckOut,
            reservaId: editingId || undefined,
          }),
          calendarioApi.events({
            imovelId: selectedImovelId,
            inicio: selectedCheckIn,
            fim: selectedCheckOut,
          }),
        ]);

        if (!active) return;

        const events = (eventsResponse.data || []).filter((event) => event.id !== `reserva-${editingId}`);
        const blockingEvents = events.filter(isBlockingEvent);
        const isAvailable = Boolean(availabilityResponse.data?.disponivel) && blockingEvents.length === 0;

        setAvailability({
          status: isAvailable ? 'available' : 'conflict',
          message: isAvailable
            ? 'Período livre para este imóvel.'
            : 'Período indisponível para este imóvel.',
          events,
        });
      } catch (availabilityError) {
        if (active) {
          setAvailability({
            status: 'error',
            message: getErrorMessage(availabilityError),
            events: [],
          });
        }
      }
    }, 250);

    return () => {
      active = false;
      clearTimeout(timeout);
    };
  }, [editingId, selectedCheckIn, selectedCheckOut, selectedImovelId]);

  const startCreate = () => {
    const defaultForm = buildEmptyReserva(tenantSettings);
    setEditingId(null);
    setError('');
    setSuccess('');
    setForm({
      ...defaultForm,
      imovelId: imoveis[0]?.id ? String(imoveis[0].id) : '',
      hospedeId: hospedes[0]?.id ? String(hospedes[0].id) : '',
    });
  };

  const startEdit = (reserva) => {
    setEditingId(reserva.id);
    setError('');
    setSuccess('');
    setForm({
      imovelId: String(reserva.imovelId),
      hospedeId: String(reserva.hospedeId),
      origem: reserva.origem,
      checkIn: dateOnly(reserva.checkIn),
      checkOut: dateOnly(reserva.checkOut),
      numeroHospedes: reserva.numeroHospedes,
      valorHospedagem: reserva.valorHospedagem,
      taxaLimpeza: reserva.taxaLimpeza,
      taxaPlataforma: reserva.taxaPlataforma,
      comissaoAdministradora: reserva.comissaoAdministradora,
      status: reserva.status,
      observacoes: reserva.observacoes || '',
    });
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');

    const payload = {
      imovelId: Number(form.imovelId),
      hospedeId: Number(form.hospedeId),
      origem: Number(form.origem),
      checkIn: form.checkIn,
      checkOut: form.checkOut,
      numeroHospedes: Number(form.numeroHospedes),
      valorHospedagem: Number(form.valorHospedagem),
      taxaLimpeza: Number(form.taxaLimpeza),
      taxaPlataforma: Number(form.taxaPlataforma),
      comissaoAdministradora: Number(form.comissaoAdministradora),
      status: Number(form.status),
      observacoes: form.observacoes?.trim() || '',
    };

    try {
      const wasEditing = Boolean(editingId);
      if (wasEditing) {
        await reservasApi.update(editingId, payload);
      } else {
        await reservasApi.create(payload);
      }
      startCreate();
      setSuccess(wasEditing ? 'Reserva atualizada e agenda recalculada.' : 'Reserva criada e calendário atualizado.');
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const cancel = async (reserva) => {
    const confirmed = confirmAction(
      'Cancelar esta reserva?',
      `A reserva de ${reserva.hospedeNome} em ${reserva.imovelNome} será marcada como cancelada. O período voltará a ficar disponível conforme as regras do calendário.`,
    );

    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');
    try {
      await reservasApi.cancel(reserva.id);
      setSuccess('Reserva cancelada e calendário atualizado.');
      await load();
    } catch (cancelError) {
      setError(getErrorMessage(cancelError));
    }
  };

  return (
    <div className="resource-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Operação</span>
          <h1>Reservas</h1>
          <p>Crie reservas com disponibilidade, hóspede, origem, valores e bloqueio de conflitos por imóvel.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
          <button className="primary-action" type="button" onClick={startCreate}>
            <Plus size={18} />
            Nova reserva
          </button>
        </div>
      </section>

      <section className="resource-layout">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <label className="search-field">
              <Search size={18} />
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por imóvel, hóspede, origem ou status" />
            </label>
            <span>{filteredReservas.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {success && <div className="form-success">{success}</div>}
          {loading ? (
            <div className="loading-line">Carregando reservas...</div>
          ) : filteredReservas.length === 0 ? (
            <EmptyState
              icon={<CalendarDays size={26} />}
              title={search.trim() ? 'Nenhuma reserva encontrada' : 'Nenhuma reserva cadastrada'}
              description={
                search.trim()
                  ? 'Ajuste a busca ou limpe o campo para voltar a ver todas as reservas.'
                  : 'A primeira reserva já movimenta calendário, financeiro, limpeza e repasses.'
              }
              actions={[
                { label: 'Preencher reserva', onClick: scrollToForm, icon: <Plus size={17} /> },
                { label: 'Ver calendário', to: '/calendario', variant: 'secondary' },
              ]}
            />
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Reserva</th>
                    <th>Período</th>
                    <th>Origem</th>
                    <th>Hóspedes</th>
                    <th>Bruto</th>
                    <th>Líquido</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {filteredReservas.map((reserva) => (
                    <tr key={reserva.id}>
                      <td>
                        <strong>{reserva.imovelNome}</strong>
                        <small>{reserva.hospedeNome}</small>
                      </td>
                      <td>
                        {formatDate(reserva.checkIn)} até {formatDate(reserva.checkOut)}
                      </td>
                      <td>{labelFor(origemOptions, reserva.origem)}</td>
                      <td>{reserva.numeroHospedes}</td>
                      <td>{money(calculateTotalReserva(reserva))}</td>
                      <td>{money(reserva.valorLiquido)}</td>
                      <td>
                        <StatusPill status={reserva.status} />
                      </td>
                      <td className="table-actions">
                        <button type="button" aria-label="Editar" onClick={() => startEdit(reserva)}>
                          <Edit3 size={16} />
                        </button>
                        <button type="button" aria-label="Cancelar" onClick={() => cancel(reserva)}>
                          <XCircle size={16} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <form className="resource-form" id="reserva-form" onSubmit={save}>
          <div className="form-title">
            <CalendarDays size={18} />
            <strong>{editingId ? 'Editar reserva' : 'Nova reserva'}</strong>
          </div>
          <div className="form-grid">
            <SelectField label="Imóvel" value={form.imovelId} onChange={(imovelId) => setForm((current) => ({ ...current, imovelId }))} required>
              <option value="">Selecione</option>
              {imoveis.map((imovel) => (
                <option key={imovel.id} value={imovel.id}>
                  {imovel.nome}
                </option>
              ))}
            </SelectField>
            <SelectField label="Hóspede" value={form.hospedeId} onChange={(hospedeId) => setForm((current) => ({ ...current, hospedeId }))} required>
              <option value="">Selecione</option>
              {hospedes.map((hospede) => (
                <option key={hospede.id} value={hospede.id}>
                  {hospede.nome}
                </option>
              ))}
            </SelectField>
            <SelectField label="Origem" value={form.origem} onChange={(origem) => setForm((current) => ({ ...current, origem }))}>
              {origemOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </SelectField>
            <SelectField label="Status" value={form.status} onChange={(status) => setForm((current) => ({ ...current, status: Number(status) }))}>
              {statusOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </SelectField>
            <TextField label="Check-in" type="date" value={form.checkIn} onChange={(checkIn) => setForm((current) => ({ ...current, checkIn }))} required />
            <TextField label="Check-out" type="date" value={form.checkOut} onChange={(checkOut) => setForm((current) => ({ ...current, checkOut }))} required />
            <div className={`availability-card ${availability.status}`}>
              <div>
                <strong>Agenda do imóvel</strong>
                <span>{availability.message}</span>
              </div>
              {availability.events.length > 0 && (
                <ul>
                  {availability.events.map((event) => (
                    <li key={event.id}>
                      <strong>{eventTypeLabel(event.tipo)}</strong>
                      <span>
                        {event.titulo}
                        {event.descricao ? ` · ${event.descricao}` : ''} · {formatEventPeriod(event)}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <TextField
              label="Nº hóspedes"
              type="number"
              min="1"
              value={form.numeroHospedes}
              onChange={(numeroHospedes) => setForm((current) => ({ ...current, numeroHospedes }))}
              required
            />
            <MoneyField
              label="Valor da hospedagem"
              value={form.valorHospedagem}
              onChange={(valorHospedagem) => setForm((current) => ({ ...current, valorHospedagem }))}
            />
            <MoneyField
              label="Taxa de limpeza"
              value={form.taxaLimpeza}
              onChange={(taxaLimpeza) => setForm((current) => ({ ...current, taxaLimpeza }))}
            />
            <MoneyField
              label="Taxa da plataforma"
              value={form.taxaPlataforma}
              onChange={(taxaPlataforma) => setForm((current) => ({ ...current, taxaPlataforma }))}
            />
            <MoneyField
              label="Comissão da administradora"
              value={form.comissaoAdministradora}
              onChange={(comissaoAdministradora) => setForm((current) => ({ ...current, comissaoAdministradora }))}
            />
            <label className="form-field">
              <span>Diárias</span>
              <input value={calculateDiarias(form)} readOnly />
            </label>
            <label className="form-field">
              <span>Bruto por dia</span>
              <input value={money(calculateValorPorDia(calculateTotalReserva(form), form))} readOnly />
            </label>
            <label className="form-field">
              <span>Bruto total</span>
              <input value={money(calculateTotalReserva(form))} readOnly />
            </label>
            <label className="form-field">
              <span>Líquido por dia</span>
              <input value={money(calculateValorPorDia(calculateValorLiquido(form), form))} readOnly />
            </label>
            <label className="form-field">
              <span>Líquido total</span>
              <input value={money(calculateValorLiquido(form))} readOnly />
            </label>
            <TextAreaField label="Observações" value={form.observacoes} onChange={(observacoes) => setForm((current) => ({ ...current, observacoes }))} />
          </div>
          <button
            className="primary-action full"
            type="submit"
            disabled={saving || imoveis.length === 0 || hospedes.length === 0 || availability.status === 'conflict' || availability.status === 'loading'}
          >
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar reserva'}
          </button>
        </form>
      </section>
    </div>
  );
}
