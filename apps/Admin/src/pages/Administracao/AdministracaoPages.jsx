import {
  Building2,
  CheckCircle2,
  Edit3,
  KeyRound,
  RotateCcw,
  Save,
  Search,
  Settings,
  ShieldCheck,
  Trash2,
  UserCog,
  Users,
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { configuracoesApi, perfisAcessoApi, usuariosApi } from '@/api/administracao';
import { proprietariosApi } from '@/api/cadastros';

const tipoUsuarioOptions = [
  { value: 1, label: 'Administrador' },
  { value: 2, label: 'Financeiro' },
  { value: 3, label: 'Operacional' },
  { value: 4, label: 'Proprietário' },
];

const emptyUsuario = {
  nome: '',
  email: '',
  senha: '',
  tipoUsuario: 3,
  perfilAcessoId: '',
  proprietarioId: '',
  isPlatformAdmin: false,
  ativo: true,
};

function extractItems(response) {
  return response.data?.items || response.data || [];
}

function getErrorMessage(error) {
  return error.response?.data?.message || 'Não foi possível concluir a operação.';
}

function normalizeId(value) {
  return value ? Number(value) : null;
}

function StatusPill({ active, label }) {
  return <span className={`status-pill ${active ? 'active' : 'inactive'}`}>{label}</span>;
}

function PageHeader({ eyebrow, title, description, onRefresh }) {
  return (
    <section className="page-heading">
      <div>
        <span className="eyebrow">{eyebrow}</span>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      <div className="resource-actions">
        <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={onRefresh}>
          <RotateCcw size={18} />
        </button>
      </div>
    </section>
  );
}

function TextField({ label, value, onChange, required, readOnly, type = 'text', placeholder }) {
  return (
    <label className="form-field">
      <span>{label}</span>
      <input
        type={type}
        value={value ?? ''}
        onChange={(event) => onChange?.(event.target.value)}
        placeholder={placeholder}
        readOnly={readOnly}
        required={required}
      />
    </label>
  );
}

function SelectField({ label, value, onChange, children, required }) {
  return (
    <label className="form-field">
      <span>{label}</span>
      <select value={value ?? ''} onChange={(event) => onChange(event.target.value)} required={required}>
        {children}
      </select>
    </label>
  );
}

function CheckboxField({ label, checked, onChange }) {
  return (
    <label className="checkbox-field">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

export function UsuariosPage() {
  const [usuarios, setUsuarios] = useState([]);
  const [perfis, setPerfis] = useState([]);
  const [proprietarios, setProprietarios] = useState([]);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState(emptyUsuario);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const tipoUsuarioLabel = useMemo(
    () => Object.fromEntries(tipoUsuarioOptions.map((option) => [option.value, option.label])),
    [],
  );

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [usuariosResponse, perfisResponse, proprietariosResponse] = await Promise.all([
        usuariosApi.list({ search }),
        perfisAcessoApi.list(),
        proprietariosApi.list({ ativo: true }),
      ]);
      setUsuarios(extractItems(usuariosResponse));
      setPerfis(extractItems(perfisResponse));
      setProprietarios(extractItems(proprietariosResponse));
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const timeout = setTimeout(load, 250);
    return () => clearTimeout(timeout);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const startCreate = () => {
    setEditingId(null);
    setForm(emptyUsuario);
  };

  const startEdit = (usuario) => {
    setEditingId(usuario.id);
    setForm({
      nome: usuario.nome || '',
      email: usuario.email || '',
      senha: '',
      tipoUsuario: Number(usuario.tipoUsuario) || 3,
      perfilAcessoId: usuario.perfilAcessoId || '',
      proprietarioId: usuario.proprietarioId || '',
      isPlatformAdmin: usuario.isPlatformAdmin,
      ativo: usuario.ativo,
    });
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');

    const payload = {
      nome: form.nome.trim(),
      email: form.email.trim(),
      senha: form.senha.trim() || null,
      tipoUsuario: Number(form.tipoUsuario),
      perfilAcessoId: normalizeId(form.perfilAcessoId),
      proprietarioId: Number(form.tipoUsuario) === 4 ? normalizeId(form.proprietarioId) : null,
      isPlatformAdmin: form.isPlatformAdmin,
      ativo: form.ativo,
    };

    try {
      if (editingId) {
        await usuariosApi.update(editingId, payload);
      } else {
        await usuariosApi.create(payload);
      }
      startCreate();
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (id) => {
    setError('');
    try {
      await usuariosApi.deactivate(id);
      await load();
      if (editingId === id) {
        startCreate();
      }
    } catch (deactivateError) {
      setError(getErrorMessage(deactivateError));
    }
  };

  return (
    <section className="resource-page">
      <PageHeader
        eyebrow="Administração"
        title="Usuários"
        description="Controle de usuários, perfis de acesso e vínculo com proprietários."
        onRefresh={load}
      />

      {error && <div className="form-alert">{error}</div>}

      <div className="resource-layout">
        <section className="resource-panel">
          <div className="resource-panel-heading">
            <label className="search-field">
              <Search size={18} />
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por nome, e-mail ou proprietário" />
            </label>
            <span>{usuarios.length} usuários</span>
          </div>

          {loading ? (
            <div className="loading-line">Carregando usuários...</div>
          ) : usuarios.length === 0 ? (
            <div className="inline-empty">
              <Users size={34} />
              <strong>Nenhum usuário encontrado</strong>
              <span>Crie acessos para equipe financeira, operacional ou proprietários.</span>
            </div>
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Usuário</th>
                    <th>Perfil</th>
                    <th>Tipo</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {usuarios.map((usuario) => (
                    <tr key={usuario.id}>
                      <td>
                        <strong>{usuario.nome}</strong>
                        <small>{usuario.email}</small>
                        {usuario.proprietario && <small>{usuario.proprietario}</small>}
                      </td>
                      <td>{usuario.perfil || 'Sem perfil'}</td>
                      <td>{tipoUsuarioLabel[Number(usuario.tipoUsuario)] || 'Operacional'}</td>
                      <td>
                        <StatusPill active={usuario.ativo} label={usuario.ativo ? 'Ativo' : 'Inativo'} />
                      </td>
                      <td>
                        <div className="table-actions">
                          <button type="button" aria-label="Editar" onClick={() => startEdit(usuario)}>
                            <Edit3 size={16} />
                          </button>
                          <button type="button" aria-label="Inativar" onClick={() => deactivate(usuario.id)}>
                            <Trash2 size={16} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <form className="resource-form" onSubmit={save}>
          <div className="form-title">
            <UserCog size={18} />
            <strong>{editingId ? 'Editar usuário' : 'Novo usuário'}</strong>
          </div>
          <div className="form-grid">
            <TextField label="Nome" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
            <TextField label="E-mail" type="email" value={form.email} onChange={(email) => setForm((current) => ({ ...current, email }))} required />
            <TextField
              label={editingId ? 'Nova senha' : 'Senha'}
              type="password"
              value={form.senha}
              onChange={(senha) => setForm((current) => ({ ...current, senha }))}
              placeholder={editingId ? 'Manter atual' : 'Mínimo 8 caracteres'}
              required={!editingId}
            />
            <SelectField
              label="Tipo"
              value={form.tipoUsuario}
              onChange={(tipoUsuario) => setForm((current) => ({ ...current, tipoUsuario: Number(tipoUsuario) }))}
              required
            >
              {tipoUsuarioOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </SelectField>
            <SelectField label="Perfil" value={form.perfilAcessoId} onChange={(perfilAcessoId) => setForm((current) => ({ ...current, perfilAcessoId }))}>
              <option value="">Sem perfil</option>
              {perfis.map((perfil) => (
                <option key={perfil.id} value={perfil.id}>{perfil.nome}</option>
              ))}
            </SelectField>
            {Number(form.tipoUsuario) === 4 && (
              <SelectField
                label="Proprietário"
                value={form.proprietarioId}
                onChange={(proprietarioId) => setForm((current) => ({ ...current, proprietarioId }))}
                required
              >
                <option value="">Selecione</option>
                {proprietarios.map((proprietario) => (
                  <option key={proprietario.id} value={proprietario.id}>{proprietario.nome}</option>
                ))}
              </SelectField>
            )}
            <CheckboxField label="Usuário ativo" checked={form.ativo} onChange={(ativo) => setForm((current) => ({ ...current, ativo }))} />
            <CheckboxField label="Administrador da plataforma" checked={form.isPlatformAdmin} onChange={(isPlatformAdmin) => setForm((current) => ({ ...current, isPlatformAdmin }))} />
          </div>
          <button className="primary-action full" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar usuário'}
          </button>
        </form>
      </div>
    </section>
  );
}

export function ConfiguracoesPage() {
  const [data, setData] = useState(null);
  const [form, setForm] = useState({ nome: '', nomeExibicao: '', ativo: true });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await configuracoesApi.get();
      setData(response.data);
      setForm({
        nome: response.data.tenant.nome || '',
        nomeExibicao: response.data.tenant.nomeExibicao || '',
        ativo: response.data.tenant.ativo,
      });
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

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    try {
      const response = await configuracoesApi.updateTenant({
        nome: form.nome.trim(),
        nomeExibicao: form.nomeExibicao.trim(),
        ativo: form.ativo,
      });
      setData((current) => ({ ...current, tenant: response.data }));
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const resumo = data?.resumo || {};

  return (
    <section className="resource-page">
      <PageHeader
        eyebrow="Administração"
        title="Configurações"
        description="Preferências do tenant, recursos disponíveis e resumo da operação."
        onRefresh={load}
      />

      {error && <div className="form-alert">{error}</div>}

      {loading ? (
        <div className="loading-line">Carregando configurações...</div>
      ) : (
        <>
          <div className="kpi-grid secondary-kpis">
            <article className="metric-card">
              <span>Usuários</span>
              <strong>{resumo.usuarios || 0}</strong>
            </article>
            <article className="metric-card">
              <span>Imóveis</span>
              <strong>{resumo.imoveis || 0}</strong>
            </article>
            <article className="metric-card">
              <span>Reservas</span>
              <strong>{resumo.reservas || 0}</strong>
            </article>
            <article className="metric-card">
              <span>Repasses</span>
              <strong>{resumo.repasses || 0}</strong>
            </article>
          </div>

          <div className="resource-layout">
            <form className="resource-form" onSubmit={save}>
              <div className="form-title">
                <Building2 size={18} />
                <strong>Tenant atual</strong>
              </div>
              <div className="form-grid">
                <TextField label="Nome" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
                <TextField label="Nome de exibição" value={form.nomeExibicao} onChange={(nomeExibicao) => setForm((current) => ({ ...current, nomeExibicao }))} required />
                <TextField label="Slug" value={data.tenant.slug} readOnly />
                <TextField label="Domínios" value={(data.tenant.domains || []).join(', ') || 'Sem domínio dedicado'} readOnly />
                <CheckboxField label="Tenant ativo" checked={form.ativo} onChange={(ativo) => setForm((current) => ({ ...current, ativo }))} />
              </div>
              <button className="primary-action full" type="submit" disabled={saving}>
                <Save size={18} />
                {saving ? 'Salvando...' : 'Salvar configurações'}
              </button>
            </form>

            <section className="resource-panel">
              <div className="resource-panel-heading">
                <div>
                  <strong>Recursos e permissões</strong>
                  <small>Catálogo usado pelos perfis de acesso</small>
                </div>
                <span>{data.recursos?.length || 0} recursos</span>
              </div>
              <div className="status-board">
                {(data.recursos || []).map((recurso) => (
                  <div key={recurso}>
                    <small>{recurso}</small>
                    <strong><ShieldCheck size={16} /> Disponível</strong>
                  </div>
                ))}
              </div>
            </section>
          </div>

          <section className="content-grid">
            <article className="panel">
              <div className="panel-heading">
                <h2>Segurança</h2>
                <span>JWT</span>
              </div>
              <div className="status-board">
                <div>
                  <small>Autenticação</small>
                  <strong><KeyRound size={16} /> Tokens e refresh token ativos</strong>
                </div>
                <div>
                  <small>Isolamento</small>
                  <strong><CheckCircle2 size={16} /> Filtro por tenant no DbContext</strong>
                </div>
              </div>
            </article>
            <article className="panel">
              <div className="panel-heading">
                <h2>Operação</h2>
                <span>Resumo</span>
              </div>
              <div className="status-board">
                <div>
                  <small>Proprietários</small>
                  <strong><Users size={16} /> {resumo.proprietarios || 0}</strong>
                </div>
                <div>
                  <small>Movimentações financeiras</small>
                  <strong><Settings size={16} /> {resumo.movimentacoes || 0}</strong>
                </div>
              </div>
            </article>
          </section>
        </>
      )}
    </section>
  );
}
