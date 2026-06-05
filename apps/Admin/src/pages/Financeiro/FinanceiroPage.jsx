import {
  ArrowDownCircle,
  ArrowUpCircle,
  Banknote,
  Edit3,
  Plus,
  RotateCcw,
  Save,
  Trash2,
  WalletCards,
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { imoveisApi, proprietariosApi } from '@/api/cadastros';
import { categoriasFinanceirasApi, financeiroApi } from '@/api/financeiro';
import { reservasApi } from '@/api/reservas';
import { EmptyState } from '@/components/EmptyState';
import { MoneyField } from '@/components/Form/MoneyField';
import { confirmAction, getFriendlyErrorMessage } from '@/lib/uiFeedback';

const tipoOptions = [
  { value: 1, label: 'Receita' },
  { value: 2, label: 'Despesa' },
];

const currentMonthStart = new Date();
currentMonthStart.setDate(1);

const emptyMovimentacao = {
  tipo: 1,
  categoriaFinanceiraId: '',
  imovelId: '',
  reservaId: '',
  proprietarioId: '',
  data: new Date().toISOString().slice(0, 10),
  descricao: '',
  valor: '',
  observacoes: '',
};

const emptyCategoria = {
  nome: '',
  tipo: 1,
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

function nullableNumber(value) {
  return value ? Number(value) : null;
}

function scrollToForm() {
  document.getElementById('movimentacao-form')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
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

function TypePill({ tipo }) {
  const isReceita = Number(tipo) === 1;
  return <span className={`status-pill ${isReceita ? 'active' : 'inactive'}`}>{labelFor(tipoOptions, tipo)}</span>;
}

export function FinanceiroPage() {
  const [movimentacoes, setMovimentacoes] = useState([]);
  const [categorias, setCategorias] = useState([]);
  const [imoveis, setImoveis] = useState([]);
  const [proprietarios, setProprietarios] = useState([]);
  const [reservas, setReservas] = useState([]);
  const [fluxo, setFluxo] = useState({ entradas: 0, saidas: 0, saldo: 0, porCategoria: [], porDia: [] });
  const [filters, setFilters] = useState({
    inicio: currentMonthStart.toISOString().slice(0, 10),
    fim: new Date().toISOString().slice(0, 10),
    tipo: '',
    categoriaId: '',
    imovelId: '',
    proprietarioId: '',
  });
  const [form, setForm] = useState(emptyMovimentacao);
  const [categoriaForm, setCategoriaForm] = useState(emptyCategoria);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [categorySaving, setCategorySaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const categoriasFiltradas = useMemo(
    () => categorias.filter((categoria) => categoria.ativo && Number(categoria.tipo) === Number(form.tipo)),
    [categorias, form.tipo],
  );

  const filterParams = useMemo(
    () => ({
      inicio: filters.inicio || undefined,
      fim: filters.fim || undefined,
      tipo: filters.tipo || undefined,
      categoriaId: filters.categoriaId || undefined,
      imovelId: filters.imovelId || undefined,
      proprietarioId: filters.proprietarioId || undefined,
    }),
    [filters],
  );

  const load = useCallback(async (paramsOverride) => {
    setLoading(true);
    setError('');
    try {
      const params = paramsOverride || filterParams;
      const [
        movimentacoesResponse,
        fluxoResponse,
        categoriasResponse,
        imoveisResponse,
        proprietariosResponse,
        reservasResponse,
      ] = await Promise.all([
        financeiroApi.listMovimentacoes(params),
        financeiroApi.fluxoCaixa(params),
        categoriasFinanceirasApi.list({ ativo: true }),
        imoveisApi.list({ pageSize: 100 }),
        proprietariosApi.list({ ativo: true, pageSize: 100 }),
        reservasApi.list({ pageSize: 100 }),
      ]);

      setMovimentacoes(extractItems(movimentacoesResponse));
      setFluxo(fluxoResponse.data || { entradas: 0, saidas: 0, saldo: 0, porCategoria: [], porDia: [] });
      setCategorias(categoriasResponse.data || []);
      setImoveis(extractItems(imoveisResponse));
      setProprietarios(extractItems(proprietariosResponse));
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

  useEffect(() => {
    if (categoriasFiltradas.length === 0) {
      return;
    }

    const timeout = setTimeout(() => {
      const exists = categoriasFiltradas.some((categoria) => String(categoria.id) === String(form.categoriaFinanceiraId));
      if (!exists) {
        setForm((current) => ({ ...current, categoriaFinanceiraId: String(categoriasFiltradas[0].id) }));
      }
    }, 0);

    return () => clearTimeout(timeout);
  }, [categoriasFiltradas, form.categoriaFinanceiraId]);

  const startCreate = () => {
    setEditingId(null);
    setError('');
    setSuccess('');
    setForm({
      ...emptyMovimentacao,
      categoriaFinanceiraId: categoriasFiltradas[0]?.id ? String(categoriasFiltradas[0].id) : '',
    });
  };

  const startEdit = (movimentacao) => {
    setEditingId(movimentacao.id);
    setError('');
    setSuccess('');
    setForm({
      tipo: movimentacao.tipo,
      categoriaFinanceiraId: String(movimentacao.categoriaFinanceiraId),
      imovelId: movimentacao.imovelId ? String(movimentacao.imovelId) : '',
      reservaId: movimentacao.reservaId ? String(movimentacao.reservaId) : '',
      proprietarioId: movimentacao.proprietarioId ? String(movimentacao.proprietarioId) : '',
      data: dateOnly(movimentacao.data),
      descricao: movimentacao.descricao || '',
      valor: movimentacao.valor || '',
      observacoes: movimentacao.observacoes || '',
    });
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');

    if (!form.categoriaFinanceiraId) {
      setError('Selecione uma categoria financeira.');
      setSaving(false);
      return;
    }

    if (!form.descricao.trim()) {
      setError('Informe a descrição da movimentação.');
      setSaving(false);
      return;
    }

    if (Number(form.valor || 0) <= 0) {
      setError('Informe um valor maior que zero.');
      setSaving(false);
      return;
    }

    const payload = {
      tipo: Number(form.tipo),
      categoriaFinanceiraId: Number(form.categoriaFinanceiraId),
      imovelId: nullableNumber(form.imovelId),
      reservaId: nullableNumber(form.reservaId),
      proprietarioId: nullableNumber(form.proprietarioId),
      data: form.data,
      descricao: form.descricao.trim(),
      valor: Number(form.valor),
      observacoes: form.observacoes?.trim() || '',
    };

    try {
      let response;
      if (editingId) {
        response = await financeiroApi.updateMovimentacao(editingId, payload);
      } else {
        response = await financeiroApi.createMovimentacao(payload);
      }

      const saved = response.data || {};
      const savedDate = dateOnly(saved.data || form.data);
      const visibleFilters = {
        inicio: savedDate,
        fim: savedDate,
        tipo: String(saved.tipo || form.tipo),
        categoriaId: String(saved.categoriaFinanceiraId || form.categoriaFinanceiraId),
        imovelId: saved.imovelId ? String(saved.imovelId) : '',
        proprietarioId: saved.proprietarioId ? String(saved.proprietarioId) : '',
      };

      startCreate();
      setFilters(visibleFilters);
      setSuccess(`Movimentação ${editingId ? 'atualizada' : 'salva'} para ${formatDate(savedDate)}.`);
      await load({
        inicio: visibleFilters.inicio,
        fim: visibleFilters.fim,
        tipo: visibleFilters.tipo,
        categoriaId: visibleFilters.categoriaId,
        imovelId: visibleFilters.imovelId || undefined,
        proprietarioId: visibleFilters.proprietarioId || undefined,
      });
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const saveCategoria = async (event) => {
    event.preventDefault();
    setCategorySaving(true);
    setError('');
    setSuccess('');
    try {
      await categoriasFinanceirasApi.create({
        nome: categoriaForm.nome.trim(),
        tipo: Number(categoriaForm.tipo),
        ativo: true,
      });
      setCategoriaForm(emptyCategoria);
      setSuccess('Categoria criada e pronta para novos lançamentos.');
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setCategorySaving(false);
    }
  };

  const deleteMovimentacao = async (movimentacao) => {
    const confirmed = confirmAction(
      'Excluir esta movimentação?',
      `${movimentacao.descricao} no valor de ${money(movimentacao.valor)} será removida do fluxo de caixa. Essa ação não pode ser desfeita.`,
    );

    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');
    try {
      await financeiroApi.deleteMovimentacao(movimentacao.id);
      setSuccess('Movimentação excluída do fluxo de caixa.');
      await load();
    } catch (deleteError) {
      setError(getErrorMessage(deleteError));
    }
  };

  return (
    <div className="resource-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Gestão financeira</span>
          <h1>Financeiro</h1>
          <p>Controle entradas, saídas, categorias e fluxo de caixa por período, imóvel e proprietário.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={() => load()}>
            <RotateCcw size={18} />
          </button>
          <button className="primary-action" type="button" onClick={startCreate}>
            <Plus size={18} />
            Nova movimentação
          </button>
        </div>
      </section>

      <section className="kpi-grid">
        <article className="metric-card">
          <div className="metric-icon green">
            <ArrowUpCircle size={19} />
          </div>
          <span>Entradas no período</span>
          <strong>{money(fluxo.entradas)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon red">
            <ArrowDownCircle size={19} />
          </div>
          <span>Saídas no período</span>
          <strong>{money(fluxo.saidas)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon blue">
            <WalletCards size={19} />
          </div>
          <span>Saldo operacional</span>
          <strong>{money(fluxo.saldo)}</strong>
        </article>
        <article className="metric-card">
          <div className="metric-icon yellow">
            <Banknote size={19} />
          </div>
          <span>Movimentações</span>
          <strong>{movimentacoes.length}</strong>
        </article>
      </section>

      <section className="resource-panel financial-filters">
        <TextField label="Início" type="date" value={filters.inicio} onChange={(inicio) => setFilters((current) => ({ ...current, inicio }))} />
        <TextField label="Fim" type="date" value={filters.fim} onChange={(fim) => setFilters((current) => ({ ...current, fim }))} />
        <SelectField label="Tipo" value={filters.tipo} onChange={(tipo) => setFilters((current) => ({ ...current, tipo }))}>
          <option value="">Todos</option>
          {tipoOptions.map((tipo) => (
            <option key={tipo.value} value={tipo.value}>
              {tipo.label}
            </option>
          ))}
        </SelectField>
        <SelectField
          label="Categoria"
          value={filters.categoriaId}
          onChange={(categoriaId) => setFilters((current) => ({ ...current, categoriaId }))}
        >
          <option value="">Todas</option>
          {categorias.map((categoria) => (
            <option key={categoria.id} value={categoria.id}>
              {categoria.nome}
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
      </section>

      <section className="resource-layout financial-layout">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Movimentações</strong>
              <small>Receitas e despesas lançadas no caixa.</small>
            </div>
            <span>{movimentacoes.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {success && <div className="form-success">{success}</div>}
          {loading ? (
            <div className="loading-line">Carregando movimentações...</div>
          ) : movimentacoes.length === 0 ? (
            <EmptyState
              icon={<Banknote size={26} />}
              title="Nenhuma movimentação no período"
              description="Ajuste os filtros ou registre uma receita/despesa para enxergar o caixa real."
              actions={[{ label: 'Nova movimentação', onClick: scrollToForm, icon: <Plus size={17} /> }]}
            />
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Data</th>
                    <th>Descrição</th>
                    <th>Categoria</th>
                    <th>Vínculo</th>
                    <th>Tipo</th>
                    <th>Valor</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {movimentacoes.map((movimentacao) => (
                    <tr key={movimentacao.id}>
                      <td>{formatDate(movimentacao.data)}</td>
                      <td>
                        <strong>{movimentacao.descricao}</strong>
                        <small>{movimentacao.observacoes || 'Sem observações'}</small>
                      </td>
                      <td>{movimentacao.categoriaNome}</td>
                      <td>
                        <strong>{movimentacao.imovelNome || movimentacao.proprietarioNome || '-'}</strong>
                        <small>{movimentacao.reservaId ? `Reserva #${movimentacao.reservaId}` : 'Sem reserva vinculada'}</small>
                      </td>
                      <td>
                        <TypePill tipo={movimentacao.tipo} />
                      </td>
                      <td>{money(movimentacao.valor)}</td>
                      <td className="table-actions">
                        <button type="button" aria-label="Editar" onClick={() => startEdit(movimentacao)}>
                          <Edit3 size={16} />
                        </button>
                        <button type="button" aria-label="Excluir" onClick={() => deleteMovimentacao(movimentacao)}>
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
          <form className="resource-form" id="movimentacao-form" onSubmit={save}>
            <div className="form-title">
              <Plus size={18} />
              <strong>{editingId ? 'Editar movimentação' : 'Nova movimentação'}</strong>
            </div>
            <div className="form-grid">
              <SelectField
                label="Tipo"
                value={form.tipo}
                onChange={(tipo) =>
                  setForm((current) => ({
                    ...current,
                    tipo: Number(tipo),
                    categoriaFinanceiraId: '',
                  }))
                }
                required
              >
                {tipoOptions.map((tipo) => (
                  <option key={tipo.value} value={tipo.value}>
                    {tipo.label}
                  </option>
                ))}
              </SelectField>
              <SelectField
                label="Categoria"
                value={form.categoriaFinanceiraId}
                onChange={(categoriaFinanceiraId) => setForm((current) => ({ ...current, categoriaFinanceiraId }))}
                required
              >
                <option value="">Selecione</option>
                {categoriasFiltradas.map((categoria) => (
                  <option key={categoria.id} value={categoria.id}>
                    {categoria.nome}
                  </option>
                ))}
              </SelectField>
              <TextField label="Data" type="date" value={form.data} onChange={(data) => setForm((current) => ({ ...current, data }))} required />
              <MoneyField
                label="Valor"
                value={form.valor}
                onChange={(valor) => setForm((current) => ({ ...current, valor }))}
                required
              />
              <TextField
                label="Descrição"
                value={form.descricao}
                onChange={(descricao) => setForm((current) => ({ ...current, descricao }))}
                required
              />
              <SelectField label="Imóvel" value={form.imovelId} onChange={(imovelId) => setForm((current) => ({ ...current, imovelId }))}>
                <option value="">Sem vínculo</option>
                {imoveis.map((imovel) => (
                  <option key={imovel.id} value={imovel.id}>
                    {imovel.nome}
                  </option>
                ))}
              </SelectField>
              <SelectField
                label="Proprietário"
                value={form.proprietarioId}
                onChange={(proprietarioId) => setForm((current) => ({ ...current, proprietarioId }))}
              >
                <option value="">Sem vínculo</option>
                {proprietarios.map((proprietario) => (
                  <option key={proprietario.id} value={proprietario.id}>
                    {proprietario.nome}
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
              <TextAreaField label="Observações" value={form.observacoes} onChange={(observacoes) => setForm((current) => ({ ...current, observacoes }))} />
            </div>
            <button className="primary-action full" type="submit" disabled={saving}>
              <Save size={18} />
              {saving ? 'Salvando...' : 'Salvar movimentação'}
            </button>
          </form>

          <form className="resource-form" onSubmit={saveCategoria}>
            <div className="form-title">
              <Banknote size={18} />
              <strong>Nova categoria</strong>
            </div>
            <div className="form-grid">
              <SelectField
                label="Tipo"
                value={categoriaForm.tipo}
                onChange={(tipo) => setCategoriaForm((current) => ({ ...current, tipo: Number(tipo) }))}
                required
              >
                {tipoOptions.map((tipo) => (
                  <option key={tipo.value} value={tipo.value}>
                    {tipo.label}
                  </option>
                ))}
              </SelectField>
              <TextField
                label="Nome"
                value={categoriaForm.nome}
                onChange={(nome) => setCategoriaForm((current) => ({ ...current, nome }))}
                required
              />
            </div>
            <button className="primary-action full" type="submit" disabled={categorySaving}>
              <Plus size={18} />
              {categorySaving ? 'Criando...' : 'Criar categoria'}
            </button>
          </form>
        </aside>
      </section>
    </div>
  );
}
