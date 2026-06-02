import { Banknote, CheckCircle2, Download, FileText, Plus, RotateCcw, Save, Trash2 } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { imoveisApi, proprietariosApi } from '@/api/cadastros';
import { relatoriosApi } from '@/api/relatorios';
import { repassesApi } from '@/api/repasses';
import { MoneyField } from '@/components/Form/MoneyField';

const statusOptions = [
  { value: 1, label: 'Pendente' },
  { value: 2, label: 'Pago' },
  { value: 3, label: 'Parcialmente pago' },
];

const currentMonthStart = new Date();
currentMonthStart.setDate(1);

const emptyGenerateForm = {
  proprietarioId: '',
  imovelId: '',
  periodoInicio: currentMonthStart.toISOString().slice(0, 10),
  periodoFim: new Date().toISOString().slice(0, 10),
  observacoes: '',
};

const emptyPaymentForm = {
  repasseId: '',
  valor: '',
  dataPagamento: new Date().toISOString().slice(0, 10),
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
  const className = Number(status) === 2 ? 'active' : 'inactive';
  return <span className={`status-pill ${className}`}>{labelFor(statusOptions, status)}</span>;
}

export function RepassesPage() {
  const [repasses, setRepasses] = useState([]);
  const [proprietarios, setProprietarios] = useState([]);
  const [imoveis, setImoveis] = useState([]);
  const [filters, setFilters] = useState({
    inicio: currentMonthStart.toISOString().slice(0, 10),
    fim: new Date().toISOString().slice(0, 10),
    proprietarioId: '',
    imovelId: '',
    status: '',
  });
  const [generateForm, setGenerateForm] = useState(emptyGenerateForm);
  const [paymentForm, setPaymentForm] = useState(emptyPaymentForm);
  const [selectedRepasse, setSelectedRepasse] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [paying, setPaying] = useState(false);
  const [error, setError] = useState('');

  const filterParams = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
      proprietarioId: filters.proprietarioId || undefined,
      imovelId: filters.imovelId || undefined,
      status: filters.status || undefined,
    }),
    [filters],
  );

  const resumo = useMemo(
    () =>
      repasses.reduce(
        (acc, repasse) => ({
          valorRepassar: acc.valorRepassar + Number(repasse.valorRepassar || 0),
          valorPago: acc.valorPago + Number(repasse.valorPago || 0),
          saldoPendente: acc.saldoPendente + Number(repasse.saldoPendente || 0),
        }),
        { valorRepassar: 0, valorPago: 0, saldoPendente: 0 },
      ),
    [repasses],
  );

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [repassesResponse, proprietariosResponse, imoveisResponse] = await Promise.all([
        repassesApi.list(filterParams),
        proprietariosApi.list({ ativo: true, pageSize: 100 }),
        imoveisApi.list({ pageSize: 100 }),
      ]);
      setRepasses(extractItems(repassesResponse));
      setProprietarios(extractItems(proprietariosResponse));
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

  const gerar = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');

    try {
      await repassesApi.gerar({
        proprietarioId: Number(generateForm.proprietarioId),
        imovelId: generateForm.imovelId ? Number(generateForm.imovelId) : null,
        periodoInicio: generateForm.periodoInicio,
        periodoFim: generateForm.periodoFim,
        observacoes: generateForm.observacoes?.trim() || '',
      });
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const startPayment = (repasse) => {
    setSelectedRepasse(repasse);
    setPaymentForm({
      repasseId: String(repasse.id),
      valor: repasse.saldoPendente,
      dataPagamento: new Date().toISOString().slice(0, 10),
      observacoes: '',
    });
  };

  const pagar = async (event) => {
    event.preventDefault();
    if (!paymentForm.repasseId) return;

    setPaying(true);
    setError('');
    try {
      await repassesApi.registrarPagamento(paymentForm.repasseId, {
        valor: Number(paymentForm.valor),
        dataPagamento: paymentForm.dataPagamento,
        observacoes: paymentForm.observacoes?.trim() || '',
      });
      setSelectedRepasse(null);
      setPaymentForm(emptyPaymentForm);
      await load();
    } catch (paymentError) {
      setError(getErrorMessage(paymentError));
    } finally {
      setPaying(false);
    }
  };

  const remove = async (repasse) => {
    setError('');
    try {
      await repassesApi.delete(repasse.id);
      await load();
    } catch (deleteError) {
      setError(getErrorMessage(deleteError));
    }
  };

  const downloadPdf = async (repasse) => {
    setError('');
    try {
      const response = await relatoriosApi.demonstrativoRepassePdf(repasse.id);
      const blob = new Blob([response.data], { type: 'application/pdf' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `demonstrativo-repasse-${repasse.id}.pdf`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      setError(getErrorMessage(downloadError));
    }
  };

  return (
    <div className="resource-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Gestão financeira</span>
          <h1>Repasses</h1>
          <p>Geração de demonstrativos, descontos, custos e controle de pagamento aos proprietários.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
        </div>
      </section>

      <section className="kpi-grid">
        <article className="metric-card">
          <div className="metric-icon blue">
            <FileText size={19} />
          </div>
          <span>Total a repassar</span>
          <strong>{money(resumo.valorRepassar)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon green">
            <CheckCircle2 size={19} />
          </div>
          <span>Pago</span>
          <strong>{money(resumo.valorPago)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon yellow">
            <Banknote size={19} />
          </div>
          <span>Pendente</span>
          <strong>{money(resumo.saldoPendente)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon red">
            <FileText size={19} />
          </div>
          <span>Demonstrativos</span>
          <strong>{repasses.length}</strong>
        </article>
      </section>

      <section className="resource-panel financial-filters">
        <TextField label="Início" type="date" value={filters.inicio} onChange={(inicio) => setFilters((current) => ({ ...current, inicio }))} />
        <TextField label="Fim" type="date" value={filters.fim} onChange={(fim) => setFilters((current) => ({ ...current, fim }))} />
        <SelectField
          label="Proprietário"
          value={filters.proprietarioId}
          onChange={(proprietarioId) => setFilters((current) => ({ ...current, proprietarioId }))}
        >
          <option value="">Todos</option>
          {proprietarios.map((proprietario) => (
            <option key={proprietario.id} value={proprietario.id}>
              {proprietario.nome}
            </option>
          ))}
        </SelectField>
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
              <strong>Demonstrativos</strong>
              <small>Repasses gerados por proprietário e período.</small>
            </div>
            <span>{repasses.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {loading ? (
            <div className="loading-line">Carregando repasses...</div>
          ) : repasses.length === 0 ? (
            <div className="inline-empty">
              <FileText size={26} />
              <strong>Nenhum repasse gerado</strong>
              <span>Gere o primeiro demonstrativo a partir das reservas e custos do período.</span>
            </div>
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Período</th>
                    <th>Proprietário</th>
                    <th>Resumo</th>
                    <th>Valor</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {repasses.map((repasse) => (
                    <tr key={repasse.id}>
                      <td>
                        <strong>
                          {formatDate(repasse.periodoInicio)} - {formatDate(repasse.periodoFim)}
                        </strong>
                        <small>{repasse.imovelNome || 'Todos os imóveis'}</small>
                      </td>
                      <td>{repasse.proprietarioNome}</td>
                      <td>
                        <strong>
                          Receita {money(repasse.receitaReservas)} · Custos {money(repasse.custosVinculados)}
                        </strong>
                        <small>
                          Taxas {money(repasse.taxasPlataforma)} · Comissão {money(repasse.comissaoAdministradora)}
                        </small>
                      </td>
                      <td>
                        <strong>{money(repasse.valorRepassar)}</strong>
                        <small>Pendente {money(repasse.saldoPendente)}</small>
                      </td>
                      <td>
                        <StatusPill status={repasse.status} />
                      </td>
                      <td className="table-actions">
                        <button type="button" aria-label="Baixar demonstrativo PDF" onClick={() => downloadPdf(repasse)}>
                          <Download size={16} />
                        </button>
                        <button type="button" aria-label="Registrar pagamento" onClick={() => startPayment(repasse)}>
                          <CheckCircle2 size={16} />
                        </button>
                        <button type="button" aria-label="Excluir" onClick={() => remove(repasse)}>
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

        <aside className="financial-side">
          <form className="resource-form" onSubmit={gerar}>
            <div className="form-title">
              <Plus size={18} />
              <strong>Gerar repasse</strong>
            </div>
            <div className="form-grid">
              <SelectField
                label="Proprietário"
                value={generateForm.proprietarioId}
                onChange={(proprietarioId) => setGenerateForm((current) => ({ ...current, proprietarioId }))}
                required
              >
                <option value="">Selecione</option>
                {proprietarios.map((proprietario) => (
                  <option key={proprietario.id} value={proprietario.id}>
                    {proprietario.nome}
                  </option>
                ))}
              </SelectField>
              <SelectField label="Imóvel" value={generateForm.imovelId} onChange={(imovelId) => setGenerateForm((current) => ({ ...current, imovelId }))}>
                <option value="">Todos</option>
                {imoveis.map((imovel) => (
                  <option key={imovel.id} value={imovel.id}>
                    {imovel.nome}
                  </option>
                ))}
              </SelectField>
              <TextField
                label="Início"
                type="date"
                value={generateForm.periodoInicio}
                onChange={(periodoInicio) => setGenerateForm((current) => ({ ...current, periodoInicio }))}
                required
              />
              <TextField
                label="Fim"
                type="date"
                value={generateForm.periodoFim}
                onChange={(periodoFim) => setGenerateForm((current) => ({ ...current, periodoFim }))}
                required
              />
              <TextAreaField
                label="Observações"
                value={generateForm.observacoes}
                onChange={(observacoes) => setGenerateForm((current) => ({ ...current, observacoes }))}
              />
            </div>
            <button className="primary-action full" type="submit" disabled={saving}>
              <Save size={18} />
              {saving ? 'Gerando...' : 'Gerar demonstrativo'}
            </button>
          </form>

          <form className="resource-form" onSubmit={pagar}>
            <div className="form-title">
              <CheckCircle2 size={18} />
              <strong>Registrar pagamento</strong>
            </div>
            <div className="repasse-selected">
              {selectedRepasse ? (
                <>
                  <strong>{selectedRepasse.proprietarioNome}</strong>
                  <span>
                    Saldo pendente {money(selectedRepasse.saldoPendente)} · Repasse #{selectedRepasse.id}
                  </span>
                </>
              ) : (
                <span>Selecione um repasse na tabela.</span>
              )}
            </div>
            <div className="form-grid">
              <MoneyField
                label="Valor"
                value={paymentForm.valor}
                onChange={(valor) => setPaymentForm((current) => ({ ...current, valor }))}
                required
              />
              <TextField
                label="Data"
                type="date"
                value={paymentForm.dataPagamento}
                onChange={(dataPagamento) => setPaymentForm((current) => ({ ...current, dataPagamento }))}
                required
              />
              <TextAreaField
                label="Observações"
                value={paymentForm.observacoes}
                onChange={(observacoes) => setPaymentForm((current) => ({ ...current, observacoes }))}
              />
            </div>
            <button className="primary-action full" type="submit" disabled={paying || !paymentForm.repasseId}>
              <Banknote size={18} />
              {paying ? 'Registrando...' : 'Registrar pagamento'}
            </button>
          </form>
        </aside>
      </section>
    </div>
  );
}
