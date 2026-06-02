import { CheckCircle2, Edit3, Plus, RotateCcw, Save, Trash2, Wrench } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { imoveisApi } from '@/api/cadastros';
import { manutencoesApi } from '@/api/operacional';
import { MoneyField } from '@/components/Form/MoneyField';

const statusOptions = [
  { value: 1, label: 'Aberta' },
  { value: 2, label: 'Em andamento' },
  { value: 3, label: 'Resolvida' },
  { value: 4, label: 'Cancelada' },
];

const currentMonthStart = new Date();
currentMonthStart.setDate(1);

const emptyForm = {
  imovelId: '',
  categoria: '',
  descricao: '',
  responsavel: '',
  dataAbertura: new Date().toISOString().slice(0, 10),
  dataPrevista: '',
  dataResolucao: '',
  valorEstimado: '',
  valorRealizado: '',
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
  const isResolved = Number(status) === 3;
  return <span className={`status-pill ${isResolved ? 'active' : 'inactive'}`}>{labelFor(statusOptions, status)}</span>;
}

export function ManutencaoPage() {
  const [manutencoes, setManutencoes] = useState([]);
  const [imoveis, setImoveis] = useState([]);
  const [filters, setFilters] = useState({
    inicio: currentMonthStart.toISOString().slice(0, 10),
    fim: new Date().toISOString().slice(0, 10),
    imovelId: '',
    status: '',
    categoria: '',
  });
  const [form, setForm] = useState(emptyForm);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const filterParams = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
      imovelId: filters.imovelId || undefined,
      status: filters.status || undefined,
      categoria: filters.categoria || undefined,
    }),
    [filters],
  );

  const resumo = useMemo(
    () => ({
      abertas: manutencoes.filter((item) => Number(item.status) === 1).length,
      emAndamento: manutencoes.filter((item) => Number(item.status) === 2).length,
      resolvidas: manutencoes.filter((item) => Number(item.status) === 3).length,
      valor: manutencoes.reduce((total, item) => total + Number(item.valorRealizado || item.valorEstimado || 0), 0),
    }),
    [manutencoes],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [manutencoesResponse, imoveisResponse] = await Promise.all([
        manutencoesApi.list(filterParams),
        imoveisApi.list({ status: 1, pageSize: 100 }),
      ]);
      setManutencoes(extractItems(manutencoesResponse));
      setImoveis(extractItems(imoveisResponse));
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
    setForm({ ...emptyForm, imovelId: imoveis[0]?.id ? String(imoveis[0].id) : '' });
  };

  const startEdit = (manutencao) => {
    setEditingId(manutencao.id);
    setForm({
      imovelId: String(manutencao.imovelId),
      categoria: manutencao.categoria || '',
      descricao: manutencao.descricao || '',
      responsavel: manutencao.responsavel || '',
      dataAbertura: dateOnly(manutencao.dataAbertura),
      dataPrevista: dateOnly(manutencao.dataPrevista),
      dataResolucao: dateOnly(manutencao.dataResolucao),
      valorEstimado: manutencao.valorEstimado || '',
      valorRealizado: manutencao.valorRealizado || '',
      status: manutencao.status,
      observacoes: manutencao.observacoes || '',
    });
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');

    const payload = {
      imovelId: Number(form.imovelId),
      categoria: form.categoria.trim(),
      descricao: form.descricao.trim(),
      responsavel: form.responsavel?.trim() || '',
      dataAbertura: form.dataAbertura,
      dataPrevista: form.dataPrevista || null,
      dataResolucao: form.dataResolucao || null,
      valorEstimado: Number(form.valorEstimado || 0),
      valorRealizado: Number(form.valorRealizado || 0),
      status: Number(form.status),
      observacoes: form.observacoes?.trim() || '',
    };

    try {
      if (editingId) {
        await manutencoesApi.update(editingId, payload);
      } else {
        await manutencoesApi.create(payload);
      }
      startCreate();
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const cancel = async (manutencao) => {
    setError('');
    try {
      await manutencoesApi.cancel(manutencao.id);
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
          <h1>Manutenção</h1>
          <p>Controle de ocorrências, responsáveis, prazos, custos estimados e valores realizados.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
          <button className="primary-action" type="button" onClick={startCreate}>
            <Plus size={18} />
            Nova manutenção
          </button>
        </div>
      </section>

      <section className="kpi-grid">
        <article className="metric-card">
          <div className="metric-icon red">
            <Wrench size={19} />
          </div>
          <span>Abertas</span>
          <strong>{resumo.abertas}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon yellow">
            <Wrench size={19} />
          </div>
          <span>Em andamento</span>
          <strong>{resumo.emAndamento}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon green">
            <CheckCircle2 size={19} />
          </div>
          <span>Resolvidas</span>
          <strong>{resumo.resolvidas}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon blue">
            <Wrench size={19} />
          </div>
          <span>Custo total</span>
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
        <TextField label="Categoria" value={filters.categoria} onChange={(categoria) => setFilters((current) => ({ ...current, categoria }))} />
      </section>

      <section className="resource-layout financial-layout">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Ocorrências</strong>
              <small>Manutenções abertas, em execução e resolvidas.</small>
            </div>
            <span>{manutencoes.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {loading ? (
            <div className="loading-line">Carregando manutenções...</div>
          ) : manutencoes.length === 0 ? (
            <div className="inline-empty">
              <Wrench size={26} />
              <strong>Nenhuma manutenção encontrada</strong>
              <span>Registre uma ocorrência para acompanhar status, custos e responsáveis.</span>
            </div>
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Abertura</th>
                    <th>Ocorrência</th>
                    <th>Imóvel</th>
                    <th>Responsável</th>
                    <th>Valores</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {manutencoes.map((manutencao) => (
                    <tr key={manutencao.id}>
                      <td>
                        <strong>{formatDate(manutencao.dataAbertura)}</strong>
                        <small>Prevista {formatDate(manutencao.dataPrevista)}</small>
                      </td>
                      <td>
                        <strong>{manutencao.categoria}</strong>
                        <small>{manutencao.descricao}</small>
                      </td>
                      <td>{manutencao.imovelNome}</td>
                      <td>{manutencao.responsavel || '-'}</td>
                      <td>
                        <strong>{money(manutencao.valorRealizado || manutencao.valorEstimado)}</strong>
                        <small>Estimado {money(manutencao.valorEstimado)}</small>
                      </td>
                      <td>
                        <StatusPill status={manutencao.status} />
                      </td>
                      <td className="table-actions">
                        <button type="button" aria-label="Editar" onClick={() => startEdit(manutencao)}>
                          <Edit3 size={16} />
                        </button>
                        <button type="button" aria-label="Cancelar" onClick={() => cancel(manutencao)}>
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

        <form className="resource-form" onSubmit={save}>
          <div className="form-title">
            <Plus size={18} />
            <strong>{editingId ? 'Editar manutenção' : 'Nova manutenção'}</strong>
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
            <TextField label="Categoria" value={form.categoria} onChange={(categoria) => setForm((current) => ({ ...current, categoria }))} required />
            <TextField label="Responsável" value={form.responsavel} onChange={(responsavel) => setForm((current) => ({ ...current, responsavel }))} />
            <SelectField label="Status" value={form.status} onChange={(status) => setForm((current) => ({ ...current, status: Number(status) }))} required>
              {statusOptions.map((status) => (
                <option key={status.value} value={status.value}>
                  {status.label}
                </option>
              ))}
            </SelectField>
            <TextField label="Data abertura" type="date" value={form.dataAbertura} onChange={(dataAbertura) => setForm((current) => ({ ...current, dataAbertura }))} required />
            <TextField label="Data prevista" type="date" value={form.dataPrevista} onChange={(dataPrevista) => setForm((current) => ({ ...current, dataPrevista }))} />
            <TextField label="Data resolução" type="date" value={form.dataResolucao} onChange={(dataResolucao) => setForm((current) => ({ ...current, dataResolucao }))} />
            <MoneyField label="Valor estimado" value={form.valorEstimado} onChange={(valorEstimado) => setForm((current) => ({ ...current, valorEstimado }))} />
            <MoneyField label="Valor realizado" value={form.valorRealizado} onChange={(valorRealizado) => setForm((current) => ({ ...current, valorRealizado }))} />
            <TextAreaField label="Descrição" value={form.descricao} onChange={(descricao) => setForm((current) => ({ ...current, descricao }))} />
            <TextAreaField label="Observações" value={form.observacoes} onChange={(observacoes) => setForm((current) => ({ ...current, observacoes }))} />
          </div>
          <button className="primary-action full" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar manutenção'}
          </button>
        </form>
      </section>
    </div>
  );
}
