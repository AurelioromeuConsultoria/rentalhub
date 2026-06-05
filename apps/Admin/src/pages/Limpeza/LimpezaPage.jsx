import { CheckCircle2, Edit3, Plus, RotateCcw, Save, Sparkles, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { imoveisApi } from '@/api/cadastros';
import { limpezasApi } from '@/api/operacional';
import { reservasApi } from '@/api/reservas';
import { EmptyState } from '@/components/EmptyState';
import { MoneyField } from '@/components/Form/MoneyField';
import { confirmAction, getFriendlyErrorMessage } from '@/lib/uiFeedback';

const statusOptions = [
  { value: 1, label: 'Pendente' },
  { value: 2, label: 'Em andamento' },
  { value: 3, label: 'Concluída' },
  { value: 4, label: 'Cancelada' },
];

const currentMonthStart = new Date();
currentMonthStart.setDate(1);

const emptyForm = {
  imovelId: '',
  reservaId: '',
  dataPrevista: new Date().toISOString().slice(0, 10),
  responsavel: '',
  valor: '',
  status: 1,
  observacoes: '',
};

function extractItems(response) {
  return response.data?.items || [];
}

function getErrorMessage(error) {
  return getFriendlyErrorMessage(error);
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
  document.getElementById('limpeza-form')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function Field({ label, children, span = false }) {
  return (
    <label className={`form-field${span ? ' span-2' : ''}`}>
      <span>{label}</span>
      {children}
    </label>
  );
}

function TextField({ label, value, onChange, required, type = 'text', min, step }) {
  return (
    <Field label={label}>
      <input
        type={type}
        value={value ?? ''}
        min={min}
        step={step}
        onChange={(event) => onChange(event.target.value)}
        required={required}
      />
    </Field>
  );
}

function SelectField({ label, value, onChange, children, required }) {
  return (
    <Field label={label}>
      <select value={value} onChange={(event) => onChange(event.target.value)} required={required}>
        {children}
      </select>
    </Field>
  );
}

function TextAreaField({ label, value, onChange }) {
  return (
    <Field label={label} span>
      <textarea value={value ?? ''} onChange={(event) => onChange(event.target.value)} />
    </Field>
  );
}

function StatusPill({ status }) {
  const isDone = Number(status) === 3;
  return <span className={`status-pill ${isDone ? 'active' : 'inactive'}`}>{labelFor(statusOptions, status)}</span>;
}

export function LimpezaPage() {
  const [limpezas, setLimpezas] = useState([]);
  const [imoveis, setImoveis] = useState([]);
  const [reservas, setReservas] = useState([]);
  const [filters, setFilters] = useState({
    inicio: currentMonthStart.toISOString().slice(0, 10),
    fim: new Date().toISOString().slice(0, 10),
    imovelId: '',
    status: '',
  });
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const filterParams = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
      imovelId: filters.imovelId || undefined,
      status: filters.status || undefined,
    }),
    [filters],
  );

  const resumo = useMemo(
    () => ({
      pendentes: limpezas.filter((item) => Number(item.status) === 1).length,
      emAndamento: limpezas.filter((item) => Number(item.status) === 2).length,
      concluidas: limpezas.filter((item) => Number(item.status) === 3).length,
      valor: limpezas.reduce((total, item) => total + Number(item.valor || 0), 0),
    }),
    [limpezas],
  );

  const load = useCallback(async (paramsOverride) => {
    setLoading(true);
    setError('');
    try {
      const params = paramsOverride || filterParams;
      const [limpezasResponse, imoveisResponse, reservasResponse] = await Promise.all([
        limpezasApi.list(params),
        imoveisApi.list({ status: 1, pageSize: 100 }),
        reservasApi.list({ pageSize: 100 }),
      ]);
      setLimpezas(extractItems(limpezasResponse));
      setImoveis(extractItems(imoveisResponse));
      setReservas(extractItems(reservasResponse));
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [filterParams]);

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, [load]);

  const startCreate = () => {
    setEditingId(null);
    setError('');
    setSuccess('');
    setForm({ ...emptyForm, imovelId: imoveis[0]?.id ? String(imoveis[0].id) : '' });
  };

  const startEdit = (limpeza) => {
    setEditingId(limpeza.id);
    setError('');
    setSuccess('');
    setForm({
      imovelId: String(limpeza.imovelId),
      reservaId: limpeza.reservaId ? String(limpeza.reservaId) : '',
      dataPrevista: dateOnly(limpeza.dataPrevista),
      responsavel: limpeza.responsavel || '',
      valor: limpeza.valor || '',
      status: limpeza.status,
      observacoes: limpeza.observacoes || '',
    });
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');

    const payload = {
      imovelId: Number(form.imovelId),
      reservaId: form.reservaId ? Number(form.reservaId) : null,
      dataPrevista: form.dataPrevista,
      responsavel: form.responsavel.trim(),
      valor: Number(form.valor || 0),
      status: Number(form.status),
      observacoes: form.observacoes?.trim() || '',
    };

    try {
      let response;
      if (editingId) {
        response = await limpezasApi.update(editingId, payload);
      } else {
        response = await limpezasApi.create(payload);
      }

      const saved = response.data || {};
      const savedDate = dateOnly(saved.dataPrevista || form.dataPrevista);
      const visibleFilters = {
        inicio: savedDate,
        fim: savedDate,
        imovelId: String(saved.imovelId || form.imovelId),
        status: '',
      };

      setFilters(visibleFilters);
      startCreate();
      setSuccess(`Limpeza ${editingId ? 'atualizada' : 'salva'} para ${formatDate(savedDate)}.`);
      await load({
        inicio: visibleFilters.inicio,
        fim: visibleFilters.fim,
        imovelId: visibleFilters.imovelId,
        status: undefined,
      });
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const cancel = async (limpeza) => {
    const confirmed = confirmAction(
      'Cancelar esta limpeza?',
      `A limpeza de ${formatDate(limpeza.dataPrevista)} para ${limpeza.imovelNome} será marcada como cancelada.`,
    );

    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');
    try {
      await limpezasApi.cancel(limpeza.id);
      setSuccess('Limpeza cancelada.');
      await load();
    } catch (cancelError) {
      setError(getErrorMessage(cancelError));
    }
  };

  return (
    <div className="resource-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Operação diária</span>
          <h1>Limpeza</h1>
          <p>Planeje limpezas por imóvel, vincule reservas e acompanhe responsável, custo e andamento.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
          <button className="primary-action" type="button" onClick={startCreate}>
            <Plus size={18} />
            Nova limpeza
          </button>
        </div>
      </section>

      <section className="kpi-grid">
        <article className="metric-card">
          <div className="metric-icon yellow">
            <Sparkles size={19} />
          </div>
          <span>Pendentes</span>
          <strong>{resumo.pendentes}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon blue">
            <Sparkles size={19} />
          </div>
          <span>Em andamento</span>
          <strong>{resumo.emAndamento}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon green">
            <CheckCircle2 size={19} />
          </div>
          <span>Concluídas</span>
          <strong>{resumo.concluidas}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon red">
            <Sparkles size={19} />
          </div>
          <span>Custo previsto</span>
          <strong>{money(resumo.valor)}</strong>
        </article>
      </section>

      <section className="resource-panel financial-filters">
        <TextField label="Início" type="date" value={filters.inicio} onChange={(inicio) => setFilters((current) => ({ ...current, inicio }))} />
        <TextField label="Fim" type="date" value={filters.fim} onChange={(fim) => setFilters((current) => ({ ...current, fim }))} />
        <SelectField label="Imóvel" value={filters.imovelId} onChange={(imovelId) => setFilters((current) => ({ ...current, imovelId }))}>
          <option value="">Todos</option>
          {imoveis.map((imovel) => (
            <option key={imovel.id} value={imovel.id}>
              {imovel.nome}
            </option>
          ))}
        </SelectField>
        <SelectField label="Status" value={filters.status} onChange={(status) => setFilters((current) => ({ ...current, status }))}>
          <option value="">Todos</option>
          {statusOptions.map((status) => (
            <option key={status.value} value={status.value}>
              {status.label}
            </option>
          ))}
        </SelectField>
      </section>

      <section className="resource-layout financial-layout">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Agenda de limpeza</strong>
              <small>Próximas limpezas e tarefas em execução.</small>
            </div>
            <span>{limpezas.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {success && <div className="form-success">{success}</div>}
          {loading ? (
            <div className="loading-line">Carregando limpezas...</div>
          ) : limpezas.length === 0 ? (
            <EmptyState
              icon={<Sparkles size={26} />}
              title="Nenhuma limpeza encontrada"
              description="Planeje a próxima limpeza por imóvel e acompanhe responsável, data e custo."
              actions={[{ label: 'Nova limpeza', onClick: scrollToForm, icon: <Plus size={17} /> }]}
            />
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Data</th>
                    <th>Imóvel</th>
                    <th>Responsável</th>
                    <th>Reserva</th>
                    <th>Valor</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {limpezas.map((limpeza) => (
                    <tr key={limpeza.id}>
                      <td>{formatDate(limpeza.dataPrevista)}</td>
                      <td>
                        <strong>{limpeza.imovelNome}</strong>
                        <small>{limpeza.observacoes || 'Sem observações'}</small>
                      </td>
                      <td>{limpeza.responsavel}</td>
                      <td>{limpeza.reservaId ? `#${limpeza.reservaId}` : '-'}</td>
                      <td>{money(limpeza.valor)}</td>
                      <td>
                        <StatusPill status={limpeza.status} />
                      </td>
                      <td className="table-actions">
                        <button type="button" aria-label="Editar" onClick={() => startEdit(limpeza)}>
                          <Edit3 size={16} />
                        </button>
                        <button type="button" aria-label="Cancelar" onClick={() => cancel(limpeza)}>
                          <Trash2 size={16} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <form className="resource-form" id="limpeza-form" onSubmit={save}>
          <div className="form-title">
            <Plus size={18} />
            <strong>{editingId ? 'Editar limpeza' : 'Nova limpeza'}</strong>
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
            <SelectField label="Reserva" value={form.reservaId} onChange={(reservaId) => setForm((current) => ({ ...current, reservaId }))}>
              <option value="">Sem vínculo</option>
              {reservas.map((reserva) => (
                <option key={reserva.id} value={reserva.id}>
                  #{reserva.id} - {reserva.imovelNome}
                </option>
              ))}
            </SelectField>
            <TextField label="Data prevista" type="date" value={form.dataPrevista} onChange={(dataPrevista) => setForm((current) => ({ ...current, dataPrevista }))} required />
            <MoneyField label="Valor" value={form.valor} onChange={(valor) => setForm((current) => ({ ...current, valor }))} />
            <TextField label="Responsável" value={form.responsavel} onChange={(responsavel) => setForm((current) => ({ ...current, responsavel }))} required />
            <SelectField label="Status" value={form.status} onChange={(status) => setForm((current) => ({ ...current, status: Number(status) }))} required>
              {statusOptions.map((status) => (
                <option key={status.value} value={status.value}>
                  {status.label}
                </option>
              ))}
            </SelectField>
            <TextAreaField label="Observações" value={form.observacoes} onChange={(observacoes) => setForm((current) => ({ ...current, observacoes }))} />
          </div>
          <button className="primary-action full" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar limpeza'}
          </button>
        </form>
      </section>
    </div>
  );
}
