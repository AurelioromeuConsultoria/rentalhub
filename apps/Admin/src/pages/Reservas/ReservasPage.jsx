import { CalendarDays, Edit3, Plus, RotateCcw, Save, Search, XCircle } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { hospedesApi, imoveisApi } from '@/api/cadastros';
import { reservasApi } from '@/api/reservas';

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

function extractItems(response) {
  return response.data?.items || [];
}

function getErrorMessage(error) {
  return error.response?.data?.message || 'Não foi possível concluir a operação.';
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

function calculateValorLiquido(form) {
  return (
    Number(form.valorHospedagem || 0) +
    Number(form.taxaLimpeza || 0) -
    Number(form.taxaPlataforma || 0) -
    Number(form.comissaoAdministradora || 0)
  );
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
  const [search, setSearch] = useState('');
  const [form, setForm] = useState(emptyReserva);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

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
      const [reservasResponse, imoveisResponse, hospedesResponse] = await Promise.all([
        reservasApi.list(),
        imoveisApi.list({ status: 1, pageSize: 100 }),
        hospedesApi.list({ ativo: true, pageSize: 100 }),
      ]);
      setReservas(extractItems(reservasResponse));
      setImoveis(extractItems(imoveisResponse));
      setHospedes(extractItems(hospedesResponse));
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

  const startCreate = () => {
    setEditingId(null);
    setForm({
      ...emptyReserva,
      imovelId: imoveis[0]?.id ? String(imoveis[0].id) : '',
      hospedeId: hospedes[0]?.id ? String(hospedes[0].id) : '',
    });
  };

  const startEdit = (reserva) => {
    setEditingId(reserva.id);
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
      if (editingId) {
        await reservasApi.update(editingId, payload);
      } else {
        await reservasApi.create(payload);
      }
      startCreate();
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const cancel = async (reserva) => {
    setError('');
    try {
      await reservasApi.cancel(reserva.id);
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
          <p>Controle de origem, período, hóspede, valores e bloqueio automático de conflitos por imóvel.</p>
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
          {loading ? (
            <div className="loading-line">Carregando reservas...</div>
          ) : filteredReservas.length === 0 ? (
            <div className="inline-empty">
              <CalendarDays size={26} />
              <strong>Nenhuma reserva cadastrada</strong>
              <span>Cadastre imóveis e hóspedes para começar a operar a agenda.</span>
            </div>
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Reserva</th>
                    <th>Período</th>
                    <th>Origem</th>
                    <th>Hóspedes</th>
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

        <form className="resource-form" onSubmit={save}>
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
            <TextField
              label="Nº hóspedes"
              type="number"
              min="1"
              value={form.numeroHospedes}
              onChange={(numeroHospedes) => setForm((current) => ({ ...current, numeroHospedes }))}
              required
            />
            <TextField
              label="Hospedagem"
              type="number"
              min="0"
              step="0.01"
              value={form.valorHospedagem}
              onChange={(valorHospedagem) => setForm((current) => ({ ...current, valorHospedagem }))}
            />
            <TextField
              label="Taxa limpeza"
              type="number"
              min="0"
              step="0.01"
              value={form.taxaLimpeza}
              onChange={(taxaLimpeza) => setForm((current) => ({ ...current, taxaLimpeza }))}
            />
            <TextField
              label="Taxa plataforma"
              type="number"
              min="0"
              step="0.01"
              value={form.taxaPlataforma}
              onChange={(taxaPlataforma) => setForm((current) => ({ ...current, taxaPlataforma }))}
            />
            <TextField
              label="Comissão"
              type="number"
              min="0"
              step="0.01"
              value={form.comissaoAdministradora}
              onChange={(comissaoAdministradora) => setForm((current) => ({ ...current, comissaoAdministradora }))}
            />
            <label className="form-field">
              <span>Valor líquido</span>
              <input value={money(calculateValorLiquido(form))} readOnly />
            </label>
            <TextAreaField label="Observações" value={form.observacoes} onChange={(observacoes) => setForm((current) => ({ ...current, observacoes }))} />
          </div>
          <button className="primary-action full" type="submit" disabled={saving || imoveis.length === 0 || hospedes.length === 0}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar reserva'}
          </button>
        </form>
      </section>
    </div>
  );
}
