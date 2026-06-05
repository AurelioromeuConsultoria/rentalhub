import {
  Activity,
  AlertTriangle,
  Building2,
  CheckCircle2,
  ClipboardList,
  Copy,
  Database,
  DollarSign,
  Edit3,
  FileDown,
  HardDrive,
  KeyRound,
  MapPin,
  Megaphone,
  PackageCheck,
  Phone,
  RotateCcw,
  Save,
  Search,
  Send,
  Server,
  Settings,
  ShieldCheck,
  Trash2,
  UserCog,
  UserX,
  Users,
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { configuracoesApi, perfisAcessoApi, tenantsApi, usuariosApi } from '@/api/administracao';
import { proprietariosApi } from '@/api/cadastros';
import { healthApi } from '@/api/health';
import { lgpdApi } from '@/api/lgpd';
import { useAuth } from '@/context/AuthContext';
import { SELECTED_TENANT_ID_KEY, SELECTED_TENANT_SLUG_KEY } from '@/lib/authStorage';
import { TENANTS_UPDATED_EVENT } from '@/lib/tenantEvents';
import { confirmAction, getFriendlyErrorMessage } from '@/lib/uiFeedback';
import { APP_VERSION } from '@/lib/version';

const tipoUsuarioOptions = [
  { value: 1, label: 'Administrador' },
  { value: 2, label: 'Financeiro' },
  { value: 3, label: 'Operacional' },
  { value: 4, label: 'Proprietário' },
];

const recursoLabels = {
  dashboard: 'Dashboard',
  imoveis: 'Imóveis',
  proprietarios: 'Proprietários',
  hospedes: 'Hóspedes',
  reservas: 'Reservas',
  calendario: 'Calendário',
  financeiro: 'Financeiro',
  repasses: 'Repasses',
  limpezas: 'Limpeza',
  manutencoes: 'Manutenção',
  relatorios: 'Relatórios',
  'portal-proprietario': 'Portal do proprietário',
  usuarios: 'Usuários',
  'perfis-acesso': 'Perfis de acesso',
  tenants: 'Empresas',
  configuracoes: 'Configurações',
  auditoria: 'Auditoria',
};

const emptyUsuario = {
  nome: '',
  email: '',
  senha: '',
  tipoUsuario: 3,
  perfilAcessoId: '',
  proprietarioId: '',
  isPlatformAdmin: false,
  ativo: true,
  enviarConvite: false,
};

const emptyPerfil = {
  nome: '',
  descricao: '',
  ativo: true,
  permissoes: {},
};

const emptyTenant = {
  nome: '',
  nomeExibicao: '',
  slug: '',
  domainsTexto: '',
  ativo: true,
  adminNome: '',
  adminEmail: '',
  adminSenha: '',
  enviarConviteAdmin: true,
};

function extractItems(response) {
  return response.data?.items || response.data || [];
}

function getErrorMessage(error) {
  return getFriendlyErrorMessage(error);
}

function normalizeId(value) {
  return value ? Number(value) : null;
}

function splitLines(value) {
  return value
    .split('\n')
    .map((item) => item.trim())
    .filter(Boolean);
}

function onboardingLabel(status) {
  const labels = {
    'base-pendente': 'Base pendente',
    'aguardando-admin': 'Admin pendente',
    'aguardando-imovel': 'Aguardando imóvel',
    'aguardando-reserva': 'Aguardando reserva',
    operacional: 'Operacional',
  };
  return labels[status] || 'Em implantação';
}

function onboardingClass(status) {
  if (status === 'operacional') {
    return 'active';
  }
  if (status === 'base-pendente') {
    return 'inactive';
  }
  return 'pending';
}

function onboardingProgress(checklist = []) {
  const done = checklist.filter((item) => item.done).length;
  return `${done}/${checklist.length || 0}`;
}

function translateHealthStatus(status) {
  const normalizedStatus = String(status || '').toLowerCase();
  if (normalizedStatus === 'healthy') {
    return 'Saudável';
  }
  if (normalizedStatus === 'degraded') {
    return 'Atenção';
  }
  if (normalizedStatus === 'unhealthy') {
    return 'Indisponível';
  }
  return status || 'Desconhecido';
}

function healthStatusClass(status) {
  const normalizedStatus = String(status || '').toLowerCase();
  if (normalizedStatus === 'healthy') {
    return 'healthy';
  }
  if (normalizedStatus === 'degraded') {
    return 'degraded';
  }
  return 'unhealthy';
}

function formatCheckedAt(value) {
  if (!value) {
    return 'Não verificado';
  }

  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'medium',
  }).format(new Date(value));
}

function buildPermissionMap(resources, permissoes = []) {
  const source = Object.fromEntries(permissoes.map((permissao) => [permissao.recurso, permissao]));

  return Object.fromEntries(resources.map((recurso) => {
    const permissao = source[recurso] || {};

    return [recurso, {
      podeVer: Boolean(permissao.podeVer),
      podeEditar: Boolean(permissao.podeEditar),
      podeExcluir: Boolean(permissao.podeExcluir),
    }];
  }));
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
        step={type === 'number' ? '0.01' : undefined}
      />
    </label>
  );
}

function TextAreaField({ label, value, onChange, placeholder, rows = 4 }) {
  return (
    <label className="form-field span-2">
      <span>{label}</span>
      <textarea
        value={value ?? ''}
        onChange={(event) => onChange?.(event.target.value)}
        placeholder={placeholder}
        rows={rows}
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
  const { usuario: currentUser } = useAuth();
  const [usuarios, setUsuarios] = useState([]);
  const [perfis, setPerfis] = useState([]);
  const [proprietarios, setProprietarios] = useState([]);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState(emptyUsuario);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [accessLink, setAccessLink] = useState(null);

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
    setAccessLink(null);
    setError('');
    setSuccess('');
  };

  const startEdit = (usuario) => {
    setEditingId(usuario.id);
    setError('');
    setSuccess('');
    setForm({
      nome: usuario.nome || '',
      email: usuario.email || '',
      senha: '',
      tipoUsuario: Number(usuario.tipoUsuario) || 3,
      perfilAcessoId: usuario.perfilAcessoId || '',
      proprietarioId: usuario.proprietarioId || '',
      isPlatformAdmin: usuario.isPlatformAdmin,
      ativo: usuario.ativo,
      enviarConvite: false,
    });
    setAccessLink(null);
  };

  const copyAccessLink = async () => {
    if (!accessLink?.url) {
      return;
    }

    try {
      await navigator.clipboard.writeText(accessLink.url);
      setAccessLink((current) => current ? { ...current, copied: true } : current);
      setTimeout(() => {
        setAccessLink((current) => current ? { ...current, copied: false } : current);
      }, 1600);
    } catch {
      setError('Não foi possível copiar o link automaticamente.');
    }
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');

    const payload = {
      nome: form.nome.trim(),
      email: form.email.trim(),
      senha: form.senha.trim() || null,
      tipoUsuario: Number(form.tipoUsuario),
      perfilAcessoId: normalizeId(form.perfilAcessoId),
      proprietarioId: Number(form.tipoUsuario) === 4 ? normalizeId(form.proprietarioId) : null,
      isPlatformAdmin: form.isPlatformAdmin,
      ativo: form.ativo,
      enviarConvite: !editingId && form.enviarConvite,
    };

    try {
      let response;
      const wasEditing = Boolean(editingId);
      if (wasEditing) {
        response = await usuariosApi.update(editingId, payload);
      } else {
        response = await usuariosApi.create(payload);
      }
      startCreate();
      if (response?.data?.conviteUrl) {
        setAccessLink({ url: response.data.conviteUrl, expiraEm: response.data.conviteExpiraEm || null });
      }
      setSuccess(wasEditing ? 'Usuário atualizado.' : payload.enviarConvite ? 'Usuário criado e convite gerado.' : 'Usuário criado.');
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const generateInvite = async (usuario) => {
    setError('');
    setSuccess('');
    try {
      const response = await usuariosApi.generateInvite(usuario.id);
      setAccessLink({
        url: response.data?.url,
        expiraEm: response.data?.expiraEm,
        usuario: usuario.nome,
      });
      setSuccess(`Convite gerado para ${usuario.nome}.`);
    } catch (inviteError) {
      setError(getErrorMessage(inviteError));
    }
  };

  const deactivate = async (id) => {
    const target = usuarios.find((usuario) => usuario.id === id);
    const confirmed = confirmAction(
      'Inativar este usuário?',
      `${target?.nome || 'Este usuário'} perderá acesso ao RentalHub até ser reativado.`,
    );

    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');
    try {
      await usuariosApi.deactivate(id);
      await load();
      if (editingId === id) {
        startCreate();
      }
      setSuccess('Usuário inativado.');
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
      {success && <div className="form-success">{success}</div>}

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
                          <button type="button" aria-label="Gerar convite" onClick={() => generateInvite(usuario)}>
                            <KeyRound size={16} />
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
          {accessLink?.url && (
            <div className="form-info span-2">
              <div>
                <strong>Link de acesso gerado{accessLink.usuario ? ` para ${accessLink.usuario}` : ''}</strong>
                <span>Envie este link para o usuário definir a senha.</span>
              </div>
              <div className="link-copy-row">
                <input value={accessLink.url} readOnly aria-label="Link de acesso" />
                <button type="button" onClick={copyAccessLink}>
                  <Copy size={16} />
                  {accessLink.copied ? 'Copiado' : 'Copiar'}
                </button>
              </div>
            </div>
          )}
          <div className="form-grid">
            <TextField label="Nome" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
            <TextField label="E-mail" type="email" value={form.email} onChange={(email) => setForm((current) => ({ ...current, email }))} required />
            {!editingId && (
              <CheckboxField
                label="Enviar convite para definir senha"
                checked={form.enviarConvite}
                onChange={(enviarConvite) => setForm((current) => ({
                  ...current,
                  enviarConvite,
                  senha: enviarConvite ? '' : current.senha,
                }))}
              />
            )}
            <TextField
              label={editingId ? 'Nova senha' : 'Senha'}
              type="password"
              value={form.senha}
              onChange={(senha) => setForm((current) => ({ ...current, senha }))}
              placeholder={form.enviarConvite ? 'Será definida pelo convite' : editingId ? 'Manter atual' : 'Mínimo 8 caracteres'}
              readOnly={!editingId && form.enviarConvite}
              required={!editingId && !form.enviarConvite}
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
            {currentUser?.isPlatformAdmin && (
              <CheckboxField
                label="Administrador da plataforma"
                checked={form.isPlatformAdmin}
                onChange={(isPlatformAdmin) => setForm((current) => ({ ...current, isPlatformAdmin }))}
              />
            )}
          </div>
          <button className="primary-action full" type="submit" disabled={saving}>
            {form.enviarConvite && !editingId ? <Send size={18} /> : <Save size={18} />}
            {saving ? 'Salvando...' : form.enviarConvite && !editingId ? 'Criar e convidar' : 'Salvar usuário'}
          </button>
        </form>
      </div>
    </section>
  );
}

export function PerfisPage() {
  const [perfis, setPerfis] = useState([]);
  const [resources, setResources] = useState([]);
  const [form, setForm] = useState(emptyPerfil);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [perfisResponse, configuracoesResponse] = await Promise.all([
        perfisAcessoApi.list(),
        configuracoesApi.get(),
      ]);
      const nextResources = configuracoesResponse.data?.recursos || [];
      setPerfis(extractItems(perfisResponse));
      setResources(nextResources);
      setForm((current) => ({
        ...current,
        permissoes: buildPermissionMap(nextResources, Object.entries(current.permissoes).map(([recurso, permissao]) => ({
          recurso,
          ...permissao,
        }))),
      }));
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
    setError('');
    setSuccess('');
    setForm({
      ...emptyPerfil,
      permissoes: buildPermissionMap(resources),
    });
  };

  const startEdit = (perfil) => {
    setEditingId(perfil.id);
    setError('');
    setSuccess('');
    setForm({
      nome: perfil.nome || '',
      descricao: perfil.descricao || '',
      ativo: perfil.ativo,
      permissoes: buildPermissionMap(resources, perfil.permissoes || []),
    });
  };

  const togglePermission = (recurso, field) => {
    setForm((current) => {
      const previous = current.permissoes[recurso] || {};
      const next = {
        podeVer: Boolean(previous.podeVer),
        podeEditar: Boolean(previous.podeEditar),
        podeExcluir: Boolean(previous.podeExcluir),
        [field]: !previous[field],
      };

      if ((field === 'podeEditar' || field === 'podeExcluir') && next[field]) {
        next.podeVer = true;
      }

      if (field === 'podeVer' && !next.podeVer) {
        next.podeEditar = false;
        next.podeExcluir = false;
      }

      return {
        ...current,
        permissoes: {
          ...current.permissoes,
          [recurso]: next,
        },
      };
    });
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');

    const payload = {
      nome: form.nome.trim(),
      descricao: form.descricao.trim(),
      ativo: form.ativo,
      permissoes: resources.map((recurso) => ({
        recurso,
        podeVer: Boolean(form.permissoes[recurso]?.podeVer),
        podeEditar: Boolean(form.permissoes[recurso]?.podeEditar),
        podeExcluir: Boolean(form.permissoes[recurso]?.podeExcluir),
      })),
    };

    try {
      const wasEditing = Boolean(editingId);
      if (wasEditing) {
        await perfisAcessoApi.update(editingId, payload);
      } else {
        await perfisAcessoApi.create(payload);
      }
      startCreate();
      setSuccess(wasEditing ? 'Perfil atualizado.' : 'Perfil criado.');
      await load();
      window.dispatchEvent(new Event(TENANTS_UPDATED_EVENT));
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (id) => {
    const target = perfis.find((perfil) => perfil.id === id);
    const confirmed = confirmAction(
      'Inativar este perfil?',
      `${target?.nome || 'Este perfil'} deixará de ser usado para novas liberações de acesso.`,
    );

    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');
    try {
      await perfisAcessoApi.deactivate(id);
      await load();
      if (editingId === id) {
        startCreate();
      }
      setSuccess('Perfil inativado.');
    } catch (deactivateError) {
      setError(getErrorMessage(deactivateError));
    }
  };

  return (
    <section className="resource-page">
      <PageHeader
        eyebrow="Administração"
        title="Perfis de acesso"
        description="Configure permissões de visualização, edição e exclusão por recurso do sistema."
        onRefresh={load}
      />

      {error && <div className="form-alert">{error}</div>}
      {success && <div className="form-success">{success}</div>}

      <div className="resource-layout wide-detail">
        <section className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Perfis cadastrados</strong>
              <small>Use perfis para liberar módulos por função</small>
            </div>
            <span>{perfis.length} perfis</span>
          </div>

          {loading ? (
            <div className="loading-line">Carregando perfis...</div>
          ) : perfis.length === 0 ? (
            <div className="inline-empty">
              <ShieldCheck size={34} />
              <strong>Nenhum perfil encontrado</strong>
              <span>Crie perfis para administradores, financeiro e operação.</span>
            </div>
          ) : (
            <div className="profile-list">
              {perfis.map((perfil) => (
                <article className="profile-card" key={perfil.id}>
                  <div>
                    <strong>{perfil.nome}</strong>
                    <span>{perfil.descricao || 'Sem descrição'}</span>
                    <small>{(perfil.permissoes || []).filter((permissao) => permissao.podeVer).length} recursos liberados</small>
                  </div>
                  <StatusPill active={perfil.ativo} label={perfil.ativo ? 'Ativo' : 'Inativo'} />
                  <div className="table-actions">
                    <button type="button" aria-label="Editar" onClick={() => startEdit(perfil)}>
                      <Edit3 size={16} />
                    </button>
                    <button type="button" aria-label="Inativar" onClick={() => deactivate(perfil.id)}>
                      <Trash2 size={16} />
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        <form className="resource-form permissions-form" onSubmit={save}>
          <div className="form-title">
            <ShieldCheck size={18} />
            <strong>{editingId ? 'Editar perfil' : 'Novo perfil'}</strong>
          </div>
          <div className="form-grid">
            <TextField label="Nome" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
            <TextField label="Descrição" value={form.descricao} onChange={(descricao) => setForm((current) => ({ ...current, descricao }))} />
            <CheckboxField label="Perfil ativo" checked={form.ativo} onChange={(ativo) => setForm((current) => ({ ...current, ativo }))} />
          </div>

          <div className="permissions-matrix">
            <div className="permissions-row header">
              <strong>Recurso</strong>
              <span>Ver</span>
              <span>Editar</span>
              <span>Excluir</span>
            </div>
            {resources.map((recurso) => (
              <div className="permissions-row" key={recurso}>
                <strong>{recursoLabels[recurso] || recurso}</strong>
                {['podeVer', 'podeEditar', 'podeExcluir'].map((field) => (
                  <label className="permission-check" key={field}>
                    <input
                      type="checkbox"
                      checked={Boolean(form.permissoes[recurso]?.[field])}
                      onChange={() => togglePermission(recurso, field)}
                    />
                    <span />
                  </label>
                ))}
              </div>
            ))}
          </div>

          <button className="primary-action full" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar perfil'}
          </button>
        </form>
      </div>
    </section>
  );
}

export function EmpresasPage() {
  const [empresas, setEmpresas] = useState([]);
  const [form, setForm] = useState(emptyTenant);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [adminAccessLink, setAdminAccessLink] = useState('');

  const selectedTenantId = localStorage.getItem(SELECTED_TENANT_ID_KEY);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await tenantsApi.list();
      setEmpresas(extractItems(response));
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
    setForm(emptyTenant);
    setAdminAccessLink('');
    setError('');
    setSuccess('');
  };

  const startEdit = (empresa) => {
    setEditingId(empresa.id);
    setAdminAccessLink('');
    setError('');
    setSuccess('');
    setForm({
      nome: empresa.nome || '',
      nomeExibicao: empresa.nomeExibicao || '',
      slug: empresa.slug || '',
      domainsTexto: (empresa.domains || []).join('\n'),
      ativo: empresa.ativo,
      adminNome: '',
      adminEmail: '',
      adminSenha: '',
      enviarConviteAdmin: true,
    });
  };

  const selectTenant = (empresa) => {
    localStorage.setItem(SELECTED_TENANT_ID_KEY, String(empresa.id));
    localStorage.setItem(SELECTED_TENANT_SLUG_KEY, empresa.slug);
    window.location.assign('/');
  };

  const clearTenantSelection = () => {
    localStorage.removeItem(SELECTED_TENANT_ID_KEY);
    localStorage.removeItem(SELECTED_TENANT_SLUG_KEY);
    window.location.assign('/');
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');

    const payload = {
      nome: form.nome.trim(),
      nomeExibicao: form.nomeExibicao.trim(),
      slug: form.slug.trim() || null,
      domains: splitLines(form.domainsTexto),
      ativo: form.ativo,
      adminNome: editingId ? null : form.adminNome.trim() || null,
      adminEmail: editingId ? null : form.adminEmail.trim() || null,
      adminSenha: editingId ? null : form.adminSenha.trim() || null,
      enviarConviteAdmin: editingId ? true : form.enviarConviteAdmin,
    };

    try {
      const wasEditing = Boolean(editingId);
      if (wasEditing) {
        await tenantsApi.update(editingId, payload);
        startCreate();
      } else {
        const response = await tenantsApi.create(payload);
        setEditingId(null);
        setForm(emptyTenant);
        setAdminAccessLink(response.data?.adminConviteUrl || '');
      }
      setSuccess(wasEditing ? 'Empresa atualizada.' : 'Empresa criada e implantação inicial preparada.');
      await load();
      window.dispatchEvent(new Event(TENANTS_UPDATED_EVENT));
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (empresa) => {
    const confirmed = confirmAction(
      'Inativar esta empresa?',
      `${empresa.nomeExibicao || empresa.nome} perderá acesso operacional. Os dados continuam preservados para reativação futura.`,
    );

    if (!confirmed) {
      return;
    }

    setError('');
    setSuccess('');
    try {
      await tenantsApi.deactivate(empresa.id);
      if (selectedTenantId === String(empresa.id)) {
        localStorage.removeItem(SELECTED_TENANT_ID_KEY);
        localStorage.removeItem(SELECTED_TENANT_SLUG_KEY);
      }
      await load();
      window.dispatchEvent(new Event(TENANTS_UPDATED_EVENT));
      setSuccess('Empresa inativada.');
    } catch (deactivateError) {
      setError(getErrorMessage(deactivateError));
    }
  };

  return (
    <section className="resource-page">
      <PageHeader
        eyebrow="Plataforma"
        title="Empresas"
        description="Cadastre clientes da plataforma, defina domínios e acompanhe a implantação de cada empresa."
        onRefresh={load}
      />

      {error && <div className="form-alert">{error}</div>}
      {success && <div className="form-success">{success}</div>}

      <div className="resource-layout">
        <section className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Empresas cadastradas</strong>
              <small>Selecione uma empresa para operar os dados dela com isolamento completo</small>
            </div>
            <span>{empresas.length} empresas</span>
          </div>

          {loading ? (
            <div className="loading-line">Carregando empresas...</div>
          ) : empresas.length === 0 ? (
            <div className="inline-empty">
              <Building2 size={34} />
              <strong>Nenhuma empresa encontrada</strong>
              <span>Cadastre o primeiro cliente para liberar o ambiente operacional da empresa.</span>
            </div>
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Empresa</th>
                    <th>Uso</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {empresas.map((empresa) => (
                    <tr key={empresa.id}>
                      <td>
                        <strong>{empresa.nomeExibicao}</strong>
                        <small>{empresa.slug}</small>
                        {(empresa.domains || []).map((domain) => <small key={domain}>{domain}</small>)}
                      </td>
                      <td>
                        <strong>{empresa.usuarios} usuários</strong>
                        <small>{empresa.imoveis} imóveis · {empresa.reservas} reservas</small>
                        <small>Implantação {onboardingProgress(empresa.onboardingChecklist)}</small>
                      </td>
                      <td>
                        <div className="tenant-status-stack">
                          <StatusPill active={empresa.ativo} label={empresa.ativo ? 'Ativa' : 'Inativa'} />
                          <span className={`status-pill ${onboardingClass(empresa.onboardingStatus)}`}>
                            {onboardingLabel(empresa.onboardingStatus)}
                          </span>
                        </div>
                      </td>
                      <td>
                        <div className="table-actions">
                          <button type="button" aria-label="Operar empresa" onClick={() => selectTenant(empresa)}>
                            <CheckCircle2 size={16} />
                          </button>
                          <button type="button" aria-label="Editar" onClick={() => startEdit(empresa)}>
                            <Edit3 size={16} />
                          </button>
                          <button type="button" aria-label="Inativar" disabled={empresa.isRootTenant} onClick={() => deactivate(empresa)}>
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

          {selectedTenantId && (
            <button className="primary-action full tenant-clear-action" type="button" onClick={clearTenantSelection}>
              Voltar para empresa do meu login
            </button>
          )}
        </section>

        <form className="resource-form" onSubmit={save}>
          <div className="form-title">
            <Building2 size={18} />
            <strong>{editingId ? 'Editar empresa' : 'Nova empresa'}</strong>
          </div>
          {!editingId && (
            <div className="form-info">
              <strong>Implantação automática</strong>
              <span>Ao salvar, o RentalHub cria perfis, categorias financeiras e envia convite para o administrador definir a senha.</span>
            </div>
          )}
          {adminAccessLink && (
            <div className="form-info">
              <strong>Link de convite do admin</strong>
              <span>Use este link se o envio por e-mail ainda não estiver configurado no servidor.</span>
              <div className="link-copy-row">
                <input value={adminAccessLink} readOnly aria-label="Link de convite do admin" />
                <button type="button" onClick={() => navigator.clipboard?.writeText(adminAccessLink)}>
                  <Copy size={16} />
                  Copiar
                </button>
              </div>
            </div>
          )}
          <div className="form-grid">
            <TextField label="Nome jurídico" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
            <TextField label="Nome de exibição" value={form.nomeExibicao} onChange={(nomeExibicao) => setForm((current) => ({ ...current, nomeExibicao }))} required />
            <TextField label="Identificador da empresa" value={form.slug} onChange={(slug) => setForm((current) => ({ ...current, slug }))} placeholder="Gerado automaticamente se ficar vazio" />
            <label className="form-field">
              <span>Domínios</span>
              <textarea
                value={form.domainsTexto}
                onChange={(event) => setForm((current) => ({ ...current, domainsTexto: event.target.value }))}
                placeholder="Um domínio por linha"
              />
            </label>
            {!editingId && (
              <>
                <TextField label="Nome do admin" value={form.adminNome} onChange={(adminNome) => setForm((current) => ({ ...current, adminNome }))} />
                <TextField label="E-mail do admin" type="email" value={form.adminEmail} onChange={(adminEmail) => setForm((current) => ({ ...current, adminEmail }))} />
                <CheckboxField
                  label="Enviar convite para o admin definir senha"
                  checked={form.enviarConviteAdmin}
                  onChange={(enviarConviteAdmin) => setForm((current) => ({
                    ...current,
                    enviarConviteAdmin,
                    adminSenha: enviarConviteAdmin ? '' : current.adminSenha,
                  }))}
                />
                {!form.enviarConviteAdmin && (
                  <TextField label="Senha do admin" type="password" value={form.adminSenha} onChange={(adminSenha) => setForm((current) => ({ ...current, adminSenha }))} placeholder="Mínimo 8 caracteres" />
                )}
              </>
            )}
            <CheckboxField label="Empresa ativa" checked={form.ativo} onChange={(ativo) => setForm((current) => ({ ...current, ativo }))} />
          </div>
          <button className="primary-action full" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar empresa'}
          </button>
        </form>
      </div>
    </section>
  );
}

export function ConfiguracoesPage() {
  const [data, setData] = useState(null);
  const [form, setForm] = useState({
    nome: '',
    nomeExibicao: '',
    documentoEmpresa: '',
    responsavelOperacional: '',
    emailOperacional: '',
    telefoneOperacional: '',
    whatsappOperacional: '',
    cep: '',
    logradouro: '',
    numero: '',
    complemento: '',
    bairro: '',
    cidade: '',
    estado: '',
    checkInPadrao: '',
    checkOutPadrao: '',
    comissaoPadraoAdministradora: '',
    taxaLimpezaPadrao: '',
    observacoesOperacionais: '',
    suporteEmail: '',
    suporteWhatsapp: '',
    suporteHorario: '',
    janelaAtualizacao: '',
    avisoAtualizacaoTitulo: '',
    avisoAtualizacaoMensagem: '',
    avisoAtualizacaoVersao: APP_VERSION,
    avisoAtualizacaoAtivo: false,
    ativo: true,
  });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [health, setHealth] = useState(null);
  const [healthError, setHealthError] = useState('');
  const [checkingHealth, setCheckingHealth] = useState(false);
  const [privacyForm, setPrivacyForm] = useState({
    tipo: 'hospede',
    id: '',
    motivo: '',
  });
  const [privacyResult, setPrivacyResult] = useState(null);
  const [privacyAction, setPrivacyAction] = useState('');
  const [privacyLoading, setPrivacyLoading] = useState(false);

  const loadHealth = useCallback(async () => {
    setCheckingHealth(true);
    setHealthError('');
    try {
      const response = await healthApi.get();
      setHealth(response.data);
    } catch (loadHealthError) {
      setHealth(null);
      setHealthError(getErrorMessage(loadHealthError));
    } finally {
      setCheckingHealth(false);
    }
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const response = await configuracoesApi.get();
      setData(response.data);
      setForm({
        nome: response.data.tenant.nome || '',
        nomeExibicao: response.data.tenant.nomeExibicao || '',
        documentoEmpresa: response.data.tenant.documentoEmpresa || '',
        responsavelOperacional: response.data.tenant.responsavelOperacional || '',
        emailOperacional: response.data.tenant.emailOperacional || '',
        telefoneOperacional: response.data.tenant.telefoneOperacional || '',
        whatsappOperacional: response.data.tenant.whatsappOperacional || '',
        cep: response.data.tenant.cep || '',
        logradouro: response.data.tenant.logradouro || '',
        numero: response.data.tenant.numero || '',
        complemento: response.data.tenant.complemento || '',
        bairro: response.data.tenant.bairro || '',
        cidade: response.data.tenant.cidade || '',
        estado: response.data.tenant.estado || '',
        checkInPadrao: response.data.tenant.checkInPadrao || '',
        checkOutPadrao: response.data.tenant.checkOutPadrao || '',
        comissaoPadraoAdministradora: response.data.tenant.comissaoPadraoAdministradora ?? '',
        taxaLimpezaPadrao: response.data.tenant.taxaLimpezaPadrao ?? '',
        observacoesOperacionais: response.data.tenant.observacoesOperacionais || '',
        suporteEmail: response.data.tenant.suporteEmail || '',
        suporteWhatsapp: response.data.tenant.suporteWhatsapp || '',
        suporteHorario: response.data.tenant.suporteHorario || '',
        janelaAtualizacao: response.data.tenant.janelaAtualizacao || '',
        avisoAtualizacaoTitulo: response.data.tenant.avisoAtualizacaoTitulo || '',
        avisoAtualizacaoMensagem: response.data.tenant.avisoAtualizacaoMensagem || '',
        avisoAtualizacaoVersao: response.data.tenant.avisoAtualizacaoVersao || APP_VERSION,
        avisoAtualizacaoAtivo: Boolean(response.data.tenant.avisoAtualizacaoAtivo),
        ativo: response.data.tenant.ativo,
      });
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }

    await loadHealth();
  }, [loadHealth]);

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, [load]);

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');
    try {
      const response = await configuracoesApi.updateTenant({
        nome: form.nome.trim(),
        nomeExibicao: form.nomeExibicao.trim(),
        documentoEmpresa: form.documentoEmpresa.trim() || null,
        responsavelOperacional: form.responsavelOperacional.trim() || null,
        emailOperacional: form.emailOperacional.trim() || null,
        telefoneOperacional: form.telefoneOperacional.trim() || null,
        whatsappOperacional: form.whatsappOperacional.trim() || null,
        cep: form.cep.trim() || null,
        logradouro: form.logradouro.trim() || null,
        numero: form.numero.trim() || null,
        complemento: form.complemento.trim() || null,
        bairro: form.bairro.trim() || null,
        cidade: form.cidade.trim() || null,
        estado: form.estado.trim() || null,
        checkInPadrao: form.checkInPadrao || null,
        checkOutPadrao: form.checkOutPadrao || null,
        comissaoPadraoAdministradora: form.comissaoPadraoAdministradora === '' ? null : Number(form.comissaoPadraoAdministradora),
        taxaLimpezaPadrao: form.taxaLimpezaPadrao === '' ? null : Number(form.taxaLimpezaPadrao),
        observacoesOperacionais: form.observacoesOperacionais.trim() || null,
        suporteEmail: form.suporteEmail.trim() || null,
        suporteWhatsapp: form.suporteWhatsapp.trim() || null,
        suporteHorario: form.suporteHorario.trim() || null,
        janelaAtualizacao: form.janelaAtualizacao.trim() || null,
        avisoAtualizacaoTitulo: form.avisoAtualizacaoTitulo.trim() || null,
        avisoAtualizacaoMensagem: form.avisoAtualizacaoMensagem.trim() || null,
        avisoAtualizacaoVersao: form.avisoAtualizacaoVersao.trim() || null,
        avisoAtualizacaoAtivo: form.avisoAtualizacaoAtivo,
        ativo: form.ativo,
      });
      setData((current) => ({ ...current, tenant: response.data }));
      setSuccess('Configurações salvas.');
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const exportPrivacyData = async () => {
    setPrivacyLoading(true);
    setPrivacyAction('');
    setPrivacyResult(null);
    try {
      const response = await lgpdApi.exportData({
        tipo: privacyForm.tipo,
        id: Number(privacyForm.id),
      });
      setPrivacyResult(response.data);
      setPrivacyAction('Exportação gerada.');
    } catch (privacyError) {
      setPrivacyAction(getErrorMessage(privacyError));
    } finally {
      setPrivacyLoading(false);
    }
  };

  const anonymizePrivacyData = async () => {
    const confirmed = confirmAction(
      'Anonimizar dados pessoais?',
      'Esta ação substitui dados pessoais por valores anonimizados e deve ser usada apenas mediante solicitação formal do titular.',
    );

    if (!confirmed) {
      return;
    }

    setPrivacyLoading(true);
    setPrivacyAction('');
    try {
      const response = await lgpdApi.anonymize({
        tipo: privacyForm.tipo,
        id: Number(privacyForm.id),
        motivo: privacyForm.motivo,
      });
      setPrivacyResult(null);
      setPrivacyAction(response.data?.message || 'Dados anonimizados.');
    } catch (privacyError) {
      setPrivacyAction(getErrorMessage(privacyError));
    } finally {
      setPrivacyLoading(false);
    }
  };

  const resumo = data?.resumo || {};
  const healthChecks = health?.checks || [];
  const apiStatusClass = healthError ? 'unhealthy' : healthStatusClass(health?.status);

  return (
    <section className="resource-page">
      <PageHeader
        eyebrow="Administração"
        title="Configurações"
        description="Dados da empresa operadora, contato, endereço e parâmetros padrão da operação."
        onRefresh={load}
      />

      {error && <div className="form-alert">{error}</div>}
      {success && <div className="form-success">{success}</div>}

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

          <section className="resource-panel monitoring-panel">
            <div className="resource-panel-heading">
              <div>
                <strong>Monitoramento</strong>
                <small>Saúde da API, banco de dados e arquivos enviados pela operação</small>
              </div>
              <button className="icon-button bordered" type="button" aria-label="Verificar saúde" onClick={loadHealth} disabled={checkingHealth}>
                <RotateCcw size={18} />
              </button>
            </div>

            <div className="monitor-grid">
              <article className={`monitor-card ${apiStatusClass}`}>
                <Server size={20} />
                <span>API</span>
                <strong>{healthError ? 'Indisponível' : translateHealthStatus(health?.status)}</strong>
                <small>{healthError || `Última checagem: ${formatCheckedAt(health?.checkedAt)}`}</small>
              </article>
              <article className="monitor-card">
                <Activity size={20} />
                <span>Tempo total</span>
                <strong>{health?.totalDurationMs != null ? `${health.totalDurationMs} ms` : '--'}</strong>
                <small>{health?.environment ? `Ambiente: ${health.environment}` : 'Aguardando checagem'}</small>
              </article>
              <article className="monitor-card">
                <PackageCheck size={20} />
                <span>Versão</span>
                <strong>Admin v{APP_VERSION}</strong>
                <small>API {health?.version ? `v${health.version}` : 'não verificada'}</small>
              </article>
              <article className="monitor-card">
                <AlertTriangle size={20} />
                <span>Trace ID</span>
                <strong>Logs estruturados</strong>
                <small>Erros críticos registram um identificador para suporte</small>
              </article>
            </div>

            <div className="monitor-checks">
              {healthChecks.length === 0 ? (
                <div className="inline-empty compact">
                  <Activity size={28} />
                  <strong>{checkingHealth ? 'Verificando ambiente...' : 'Monitoramento ainda não consultado'}</strong>
                  <span>Atualize para consultar API, banco de dados e arquivos enviados.</span>
                </div>
              ) : healthChecks.map((check) => {
                const Icon = check.name === 'database' ? Database : HardDrive;
                return (
                  <div className={`monitor-check ${healthStatusClass(check.status)}`} key={check.name}>
                    <Icon size={18} />
                    <div>
                      <strong>{check.name === 'database' ? 'PostgreSQL' : 'Storage de uploads'}</strong>
                      <span>{check.description || translateHealthStatus(check.status)}</span>
                    </div>
                    <small>{translateHealthStatus(check.status)} · {check.durationMs ?? '--'} ms</small>
                  </div>
                );
              })}
            </div>
          </section>

          <section className="resource-panel support-settings-panel">
            <div className="resource-panel-heading">
              <div>
                <strong>Suporte e atualizações</strong>
                <small>Canais oficiais, janela de atualização e aviso exibido aos usuários</small>
              </div>
              <Megaphone size={20} />
            </div>

            <div className="support-settings-grid">
              <TextField
                label="E-mail de suporte"
                type="email"
                value={form.suporteEmail}
                onChange={(suporteEmail) => setForm((current) => ({ ...current, suporteEmail }))}
                placeholder="suporte@malachdigital.com.br"
              />
              <TextField
                label="WhatsApp de suporte"
                value={form.suporteWhatsapp}
                onChange={(suporteWhatsapp) => setForm((current) => ({ ...current, suporteWhatsapp }))}
                placeholder="(11) 99999-9999"
              />
              <TextField
                label="Horário de suporte"
                value={form.suporteHorario}
                onChange={(suporteHorario) => setForm((current) => ({ ...current, suporteHorario }))}
                placeholder="Segunda a sexta, 09h às 18h"
              />
              <TextField
                label="Janela de atualização"
                value={form.janelaAtualizacao}
                onChange={(janelaAtualizacao) => setForm((current) => ({ ...current, janelaAtualizacao }))}
                placeholder="Terças e quintas após 22h"
              />
              <TextField
                label="Título do aviso"
                value={form.avisoAtualizacaoTitulo}
                onChange={(avisoAtualizacaoTitulo) => setForm((current) => ({ ...current, avisoAtualizacaoTitulo }))}
                placeholder="Nova versão publicada"
              />
              <TextField
                label="Versão do aviso"
                value={form.avisoAtualizacaoVersao}
                onChange={(avisoAtualizacaoVersao) => setForm((current) => ({ ...current, avisoAtualizacaoVersao }))}
                placeholder={APP_VERSION}
              />
              <TextAreaField
                label="Mensagem do aviso"
                value={form.avisoAtualizacaoMensagem}
                onChange={(avisoAtualizacaoMensagem) => setForm((current) => ({ ...current, avisoAtualizacaoMensagem }))}
                placeholder="Informe em linguagem simples o que mudou e se existe alguma ação esperada do usuário."
              />
              <CheckboxField
                label="Exibir aviso de atualização para usuários"
                checked={form.avisoAtualizacaoAtivo}
                onChange={(avisoAtualizacaoAtivo) => setForm((current) => ({ ...current, avisoAtualizacaoAtivo }))}
              />
            </div>
          </section>

          <section className="resource-panel privacy-panel">
            <div className="resource-panel-heading">
              <div>
                <strong>Privacidade e LGPD</strong>
                <small>Exportação e anonimização de dados pessoais por solicitação do titular</small>
              </div>
              <span>Termos v2026-06-05</span>
            </div>

            <div className="privacy-grid">
              <SelectField
                label="Titular"
                value={privacyForm.tipo}
                onChange={(tipo) => setPrivacyForm((current) => ({ ...current, tipo }))}
              >
                <option value="hospede">Hóspede</option>
                <option value="proprietario">Proprietário</option>
                <option value="usuario">Usuário</option>
              </SelectField>
              <TextField
                label="ID do cadastro"
                type="number"
                value={privacyForm.id}
                onChange={(id) => setPrivacyForm((current) => ({ ...current, id }))}
                placeholder="Ex.: 12"
              />
              <TextField
                label="Motivo da anonimização"
                value={privacyForm.motivo}
                onChange={(motivo) => setPrivacyForm((current) => ({ ...current, motivo }))}
                placeholder="Ex.: solicitação formal do titular"
              />
            </div>

            <div className="button-row privacy-actions">
              <button
                className="secondary-action"
                type="button"
                disabled={privacyLoading || !privacyForm.id}
                onClick={exportPrivacyData}
              >
                <FileDown size={18} />
                Exportar dados
              </button>
              <button
                className="danger-action"
                type="button"
                disabled={privacyLoading || !privacyForm.id || privacyForm.motivo.trim().length < 8}
                onClick={anonymizePrivacyData}
              >
                <UserX size={18} />
                Anonimizar dados
              </button>
            </div>

            {privacyAction && <div className="inline-note">{privacyAction}</div>}
            {privacyResult && (
              <pre className="json-preview">{JSON.stringify(privacyResult, null, 2)}</pre>
            )}
          </section>

          <div className="resource-layout">
            <form className="resource-form" onSubmit={save}>
              <div className="form-title">
                <Building2 size={18} />
                <strong>Empresa operadora</strong>
              </div>
              <div className="form-grid">
                <TextField label="Nome" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
                <TextField label="Nome de exibição" value={form.nomeExibicao} onChange={(nomeExibicao) => setForm((current) => ({ ...current, nomeExibicao }))} required />
                <TextField label="CPF/CNPJ" value={form.documentoEmpresa} onChange={(documentoEmpresa) => setForm((current) => ({ ...current, documentoEmpresa }))} />
                <TextField label="Responsável operacional" value={form.responsavelOperacional} onChange={(responsavelOperacional) => setForm((current) => ({ ...current, responsavelOperacional }))} />
                <TextField label="E-mail operacional" type="email" value={form.emailOperacional} onChange={(emailOperacional) => setForm((current) => ({ ...current, emailOperacional }))} />
                <TextField label="Telefone" value={form.telefoneOperacional} onChange={(telefoneOperacional) => setForm((current) => ({ ...current, telefoneOperacional }))} />
                <TextField label="WhatsApp" value={form.whatsappOperacional} onChange={(whatsappOperacional) => setForm((current) => ({ ...current, whatsappOperacional }))} />
                <TextField label="Identificador da empresa" value={data.tenant.slug} readOnly />
                <TextField label="CEP" value={form.cep} onChange={(cep) => setForm((current) => ({ ...current, cep }))} />
                <TextField label="Logradouro" value={form.logradouro} onChange={(logradouro) => setForm((current) => ({ ...current, logradouro }))} />
                <TextField label="Número" value={form.numero} onChange={(numero) => setForm((current) => ({ ...current, numero }))} />
                <TextField label="Complemento" value={form.complemento} onChange={(complemento) => setForm((current) => ({ ...current, complemento }))} />
                <TextField label="Bairro" value={form.bairro} onChange={(bairro) => setForm((current) => ({ ...current, bairro }))} />
                <TextField label="Cidade" value={form.cidade} onChange={(cidade) => setForm((current) => ({ ...current, cidade }))} />
                <TextField label="Estado" value={form.estado} onChange={(estado) => setForm((current) => ({ ...current, estado: estado.toUpperCase() }))} placeholder="UF" />
                <TextField label="Domínios" value={(data.tenant.domains || []).join(', ') || 'Sem domínio dedicado'} readOnly />
                <TextField label="Check-in padrão" type="time" value={form.checkInPadrao} onChange={(checkInPadrao) => setForm((current) => ({ ...current, checkInPadrao }))} />
                <TextField label="Check-out padrão" type="time" value={form.checkOutPadrao} onChange={(checkOutPadrao) => setForm((current) => ({ ...current, checkOutPadrao }))} />
                <TextField
                  label="Comissão padrão da administradora (%)"
                  type="number"
                  value={form.comissaoPadraoAdministradora}
                  onChange={(comissaoPadraoAdministradora) => setForm((current) => ({ ...current, comissaoPadraoAdministradora }))}
                  placeholder="Ex.: 15"
                />
                <TextField
                  label="Taxa de limpeza sugerida"
                  type="number"
                  value={form.taxaLimpezaPadrao}
                  onChange={(taxaLimpezaPadrao) => setForm((current) => ({ ...current, taxaLimpezaPadrao }))}
                  placeholder="Ex.: 180.00"
                />
                <CheckboxField label="Empresa ativa" checked={form.ativo} onChange={(ativo) => setForm((current) => ({ ...current, ativo }))} />
                <TextAreaField
                  label="Observações operacionais"
                  value={form.observacoesOperacionais}
                  onChange={(observacoesOperacionais) => setForm((current) => ({ ...current, observacoesOperacionais }))}
                  placeholder="Instruções internas, política de operação, rotina de atendimento ou observações da empresa."
                />
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
                  <small>Catálogo base para perfis, menus e regras de acesso</small>
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
                <h2>Contato e presença</h2>
                <span>Operação</span>
              </div>
              <div className="status-board">
                <div>
                  <small>Canal principal</small>
                  <strong><Phone size={16} /> {data.tenant.emailOperacional || data.tenant.telefoneOperacional || 'Não configurado'}</strong>
                </div>
                <div>
                  <small>Endereço</small>
                  <strong><MapPin size={16} /> {data.tenant.cidade && data.tenant.estado ? `${data.tenant.cidade}/${data.tenant.estado}` : 'Sem cidade/UF'}</strong>
                </div>
              </div>
            </article>
            <article className="panel">
              <div className="panel-heading">
                <h2>Padrões da operação</h2>
                <span>Reservas</span>
              </div>
              <div className="status-board">
                <div>
                  <small>Horários padrão</small>
                  <strong><ClipboardList size={16} /> {data.tenant.checkInPadrao || '--:--'} / {data.tenant.checkOutPadrao || '--:--'}</strong>
                </div>
                <div>
                  <small>Comissão e limpeza</small>
                  <strong><DollarSign size={16} /> {data.tenant.comissaoPadraoAdministradora ?? 0}% · R$ {Number(data.tenant.taxaLimpezaPadrao || 0).toFixed(2).replace('.', ',')}</strong>
                </div>
              </div>
            </article>
            <article className="panel">
              <div className="panel-heading">
                <h2>Segurança</h2>
                <span>Ambiente</span>
              </div>
              <div className="status-board">
                <div>
                  <small>Autenticação</small>
                  <strong><KeyRound size={16} /> Tokens e refresh token ativos</strong>
                </div>
                <div>
                  <small>Isolamento</small>
                  <strong><CheckCircle2 size={16} /> Dados separados por empresa</strong>
                </div>
                <div>
                  <small>Administração de empresas</small>
                  <strong><ShieldCheck size={16} /> {data.tenant.podeGerenciarEmpresas ? 'Admin geral' : 'Empresa isolada'}</strong>
                </div>
              </div>
            </article>
            <article className="panel">
              <div className="panel-heading">
                <h2>Base operacional</h2>
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
