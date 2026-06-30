import { Bell, ChevronDown, Moon, RefreshCw, Search, Settings, ShieldAlert, Sun } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { tenantsApi } from '@/api/administracao';
import { buscaGlobalApi, notificacoesApi } from '@/api/operacao';
import { supportAccessApi } from '@/api/supportAccess';
import { useAuth } from '@/context/AuthContext';
import {
  SELECTED_TENANT_ID_KEY,
  SELECTED_TENANT_SLUG_KEY,
  SUPPORT_ACCESS_EXPIRES_KEY,
  SUPPORT_ACCESS_REASON_KEY,
  SUPPORT_ACCESS_TENANT_NAME_KEY,
  SUPPORT_ACCESS_TOKEN_KEY,
  clearSupportAccessStorage,
  readSupportAccessState,
} from '@/lib/authStorage';
import { TENANTS_UPDATED_EVENT } from '@/lib/tenantEvents';
import { isPlatformAdminUser } from '@/lib/platformAccess';
import { useTheme } from '@/context/ThemeContext';

const routeLabels = {
  '': 'Dashboard',
  reservas: 'Reservas',
  calendario: 'Calendário',
  imoveis: 'Imóveis',
  proprietarios: 'Sócios',
  hospedes: 'Hóspedes',
  financeiro: 'Financeiro',
  repasses: 'Repasses',
  limpeza: 'Limpeza',
  manutencao: 'Manutenção',
  relatorios: 'Relatórios',
  'portal-proprietario': 'Portal do Sócio',
  usuarios: 'Usuários',
  perfis: 'Perfis',
  empresas: 'Empresas',
  configuracoes: 'Configurações',
  auditoria: 'Auditoria',
  suporte: 'Suporte',
};

const notificationTypeLabels = {
  'nova-reserva': 'Reserva',
  checkin: 'Check-in',
  checkout: 'Check-out',
  limpeza: 'Limpeza',
  manutencao: 'Manutenção',
  repasse: 'Repasse',
};

const supportReasonOptions = [
  { value: 'cliente', label: 'Solicitação do cliente' },
  { value: 'incidente', label: 'Incidente em produção' },
  { value: 'implantacao', label: 'Implantação ou onboarding' },
  { value: 'financeiro', label: 'Conferência financeira ou repasse' },
  { value: 'qa', label: 'Validação operacional assistida' },
];

function getBreadcrumbs(pathname) {
  const segments = pathname.split('/').filter(Boolean);
  const breadcrumbs = [{ label: 'Dashboard', path: '/' }];

  let currentPath = '';
  segments.forEach((segment, index) => {
    currentPath += `/${segment}`;
    breadcrumbs.push({
      label: routeLabels[segment] || segment,
      path: index === segments.length - 1 ? null : currentPath,
    });
  });

  return breadcrumbs;
}

function formatDate(value) {
  if (!value) return '';
  const [year, month, day] = String(value).slice(0, 10).split('-');
  return `${day}/${month}/${year}`;
}

function formatNotificationDate(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return formatDate(value);
  }

  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

export function Header() {
  const location = useLocation();
  const navigate = useNavigate();
  const { usuario, currentTenant, logout } = useAuth();
  const isPlatformAdmin = isPlatformAdminUser(usuario);
  const { isDark, toggleTheme } = useTheme();
  const [notifications, setNotifications] = useState([]);
  const [notificationsUpdatedAt, setNotificationsUpdatedAt] = useState(null);
  const [notificationLoading, setNotificationLoading] = useState(false);
  const [showNotifications, setShowNotifications] = useState(false);
  const [search, setSearch] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [showSearch, setShowSearch] = useState(false);
  const [tenants, setTenants] = useState([]);
  const [showTenants, setShowTenants] = useState(false);
  const [selectedTenantId, setSelectedTenantId] = useState(() => localStorage.getItem(SELECTED_TENANT_ID_KEY));
  const [supportReason, setSupportReason] = useState(() => localStorage.getItem(SUPPORT_ACCESS_REASON_KEY) || '');
  const [supportExpiresAt, setSupportExpiresAt] = useState(() => localStorage.getItem(SUPPORT_ACCESS_EXPIRES_KEY) || '');
  const [supportTenantName, setSupportTenantName] = useState(() => localStorage.getItem(SUPPORT_ACCESS_TENANT_NAME_KEY) || '');
  const [tenantSwitching, setTenantSwitching] = useState(false);
  const [supportCategory, setSupportCategory] = useState(supportReasonOptions[0].value);
  const [supportDetails, setSupportDetails] = useState('');
  const [supportTargetTenantId, setSupportTargetTenantId] = useState('');
  const [supportError, setSupportError] = useState('');
  const breadcrumbs = getBreadcrumbs(location.pathname);
  const highPriorityCount = useMemo(
    () => notifications.filter((item) => item.prioridade === 'alta').length,
    [notifications],
  );
  const visibleNotifications = useMemo(() => notifications.slice(0, 8), [notifications]);
  const tenantOptions = useMemo(
    () => tenants.filter((tenant) => String(tenant.id) !== String(currentTenant?.id)),
    [currentTenant?.id, tenants],
  );
  const initials = usuario?.nome
    ?.split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase() || 'RH';
  const activeTenant = useMemo(() => {
    const selectedTenant = tenants.find((tenant) => String(tenant.id) === String(selectedTenantId));
    return selectedTenant || currentTenant;
  }, [currentTenant, selectedTenantId, tenants]);
  const supportState = useMemo(() => {
    const expiresDate = supportExpiresAt ? new Date(supportExpiresAt) : null;
    const isExpired = Boolean(expiresDate && Number.isFinite(expiresDate.getTime()) && expiresDate <= new Date());
    const isActive = Boolean(
      selectedTenantId &&
      supportReason &&
      supportExpiresAt &&
      !isExpired &&
      String(selectedTenantId) !== String(currentTenant?.id),
    );

    return {
      isActive,
      isExpired,
      expiresLabel: supportExpiresAt
        ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(supportExpiresAt))
        : '',
    };
  }, [currentTenant?.id, selectedTenantId, supportExpiresAt, supportReason]);

  const syncSupportState = useCallback(() => {
    const support = readSupportAccessState(currentTenant?.id);
    if (support.isExpired) {
      clearSupportAccessStorage();
    }

    setSelectedTenantId(support.isExpired ? null : support.selectedTenantId);
    setSupportReason(support.isExpired ? '' : support.reason);
    setSupportExpiresAt(support.isExpired ? '' : support.expiresAt || '');
    setSupportTenantName(support.isExpired ? '' : support.tenantName || '');
  }, [currentTenant?.id]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const loadNotifications = useCallback(async () => {
    setNotificationLoading(true);
    try {
      const response = await notificacoesApi.list({ dias: 3, novasReservasHoras: 48 });
      setNotifications(response.data || []);
      setNotificationsUpdatedAt(new Date());
    } catch {
      setNotifications([]);
    } finally {
      setNotificationLoading(false);
    }
  }, []);

  useEffect(() => {
    const timeout = setTimeout(loadNotifications, 0);
    const interval = setInterval(loadNotifications, 60000);
    return () => {
      clearTimeout(timeout);
      clearInterval(interval);
    };
  }, [loadNotifications, location.pathname]);

  const loadTenants = useCallback(async () => {
    if (!isPlatformAdmin) {
      setTenants([]);
      return;
    }

    try {
      const response = await tenantsApi.list();
      setTenants(response.data || []);
    } catch {
      setTenants([]);
    }
  }, [isPlatformAdmin]);

  useEffect(() => {
    const timeout = setTimeout(loadTenants, 0);
    window.addEventListener(TENANTS_UPDATED_EVENT, loadTenants);

    return () => {
      clearTimeout(timeout);
      window.removeEventListener(TENANTS_UPDATED_EVENT, loadTenants);
    };
  }, [loadTenants]);

  useEffect(() => {
    const timeout = setTimeout(syncSupportState, 0);
    window.addEventListener('storage', syncSupportState);
    window.addEventListener('focus', syncSupportState);

    return () => {
      clearTimeout(timeout);
      window.removeEventListener('storage', syncSupportState);
      window.removeEventListener('focus', syncSupportState);
    };
  }, [syncSupportState]);

  useEffect(() => {
    const timeout = setTimeout(async () => {
      if (search.trim().length < 2) {
        setSearchResults([]);
        return;
      }

      try {
        const response = await buscaGlobalApi.search({ q: search.trim() });
        setSearchResults(response.data || []);
        setShowSearch(true);
      } catch {
        setSearchResults([]);
      }
    }, 220);

    return () => clearTimeout(timeout);
  }, [search]);

  const navigateTo = (href) => {
    setShowSearch(false);
    setShowNotifications(false);
    setSearch('');
    navigate(href);
  };

  const clearSupportAccess = async () => {
    const token = localStorage.getItem(SUPPORT_ACCESS_TOKEN_KEY);
    clearSupportAccessStorage();
    setSelectedTenantId(null);
    setSupportReason('');
    setSupportExpiresAt('');
    setSupportTenantName('');
    setSupportError('');
    setSupportDetails('');
    setSupportTargetTenantId('');

    if (token) {
      try {
        await supportAccessApi.end(token);
      } catch {
        // A sessão local já foi removida; falha no encerramento remoto não deve travar a troca.
      }
    }
  };

  const selectTenant = async (tenant) => {
    if (tenantSwitching) {
      return;
    }

    setSupportError('');
    setTenantSwitching(true);

    if (!tenant) {
      await clearSupportAccess();
      setShowTenants(false);
      navigate('/');
      window.location.reload();
      return;
    }

    const selectedReasonOption = supportReasonOptions.find((item) => item.value === supportCategory);
    const trimmedDetails = supportDetails.trim();
    const motivo = trimmedDetails
      ? `${selectedReasonOption?.label || 'Suporte'}: ${trimmedDetails}`
      : selectedReasonOption?.label || '';

    if (!supportCategory || !motivo || motivo.length < 10) {
      setSupportError('Escolha um motivo de suporte e descreva rapidamente o contexto do acesso.');
      setTenantSwitching(false);
      return;
    }

    try {
      await clearSupportAccess();
      const response = await supportAccessApi.start({ tenantId: tenant.id, motivo });
      localStorage.setItem(SELECTED_TENANT_ID_KEY, String(response.data.tenantId));
      localStorage.setItem(SELECTED_TENANT_SLUG_KEY, response.data.tenantSlug);
      localStorage.setItem(SUPPORT_ACCESS_TOKEN_KEY, response.data.token);
      localStorage.setItem(SUPPORT_ACCESS_REASON_KEY, response.data.motivo);
      localStorage.setItem(SUPPORT_ACCESS_EXPIRES_KEY, response.data.expiraEm);
      localStorage.setItem(SUPPORT_ACCESS_TENANT_NAME_KEY, response.data.tenantNome);
      setSupportReason(response.data.motivo);
      setSupportExpiresAt(response.data.expiraEm);
      setSupportTenantName(response.data.tenantNome);
      setSelectedTenantId(String(response.data.tenantId));
      setSupportDetails('');
      setSupportTargetTenantId(String(response.data.tenantId));
      setShowTenants(false);
      navigate('/');
      window.location.reload();
    } catch (switchError) {
      setSupportError(switchError.response?.data?.message || 'Não foi possível iniciar o modo suporte.');
      setTenantSwitching(false);
    }
  };

  const openSupportPanel = async () => {
    if (!isPlatformAdmin) {
      return;
    }

    setShowTenants((current) => !current);
    setSupportError('');
    await loadTenants();
  };

  const startSupportForSelectedTenant = async () => {
    const tenant = tenantOptions.find((item) => String(item.id) === String(supportTargetTenantId));
    if (!tenant) {
      setSupportError('Selecione a empresa que será atendida.');
      return;
    }

    await selectTenant(tenant);
  };

  return (
    <header className="topbar">
      <div className="topbar-title">
        <h1>RentalHub</h1>
        {breadcrumbs.length > 1 && (
          <nav className="breadcrumbs" aria-label="Breadcrumbs">
            {breadcrumbs.map((breadcrumb, index) => (
              <span key={`${breadcrumb.label}-${index}`}>
                {index > 0 && <span className="breadcrumb-separator">/</span>}
                {breadcrumb.path ? (
                  <Link to={breadcrumb.path}>{breadcrumb.label}</Link>
                ) : (
                  <strong>{breadcrumb.label}</strong>
                )}
              </span>
            ))}
          </nav>
        )}
      </div>

      <div className="topbar-actions">
        <div className="global-search">
          <Search size={16} />
          <input
            aria-label="Busca global"
            placeholder="Buscar"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            onFocus={() => setShowSearch(true)}
          />
          {showSearch && search.trim().length >= 2 && (
            <div className="topbar-popover search-popover">
              {searchResults.length === 0 ? (
                <span className="popover-empty">Nenhum resultado encontrado</span>
              ) : (
                searchResults.map((item) => (
                  <button type="button" key={item.id} onClick={() => navigateTo(item.href)}>
                    <strong>{item.titulo}</strong>
                    <span>{item.tipo}{item.descricao ? ` · ${item.descricao}` : ''}</span>
                  </button>
                ))
              )}
            </div>
          )}
        </div>
        <button
          className="icon-button"
          type="button"
          aria-label={isDark ? 'Ativar tema claro' : 'Ativar tema escuro'}
          title={isDark ? 'Ativar tema claro' : 'Ativar tema escuro'}
          onClick={toggleTheme}
        >
          {isDark ? <Sun size={18} /> : <Moon size={18} />}
        </button>
        <div className="notification-anchor">
          <button
            className={`icon-button notification-button${notifications.length > 0 ? ' has-notifications' : ''}`}
            type="button"
            aria-label="Notificações"
            onClick={() => setShowNotifications((current) => !current)}
          >
            <Bell size={18} />
            {notifications.length > 0 && <span>{highPriorityCount || notifications.length}</span>}
          </button>
          {showNotifications && (
            <div className="topbar-popover notification-popover">
              <div className="popover-heading">
                <div>
                  <strong>Notificações</strong>
                  {notificationsUpdatedAt && (
                    <small>Atualizado {formatNotificationDate(notificationsUpdatedAt)}</small>
                  )}
                </div>
                <button type="button" onClick={loadNotifications} disabled={notificationLoading}>
                  <RefreshCw size={13} />
                  {notificationLoading ? 'Atualizando' : 'Atualizar'}
                </button>
              </div>
              {notifications.length === 0 ? (
                <span className="popover-empty">Nenhuma pendência crítica</span>
              ) : (
                visibleNotifications.map((item) => (
                  <button
                    className={`notification-item ${item.prioridade}`}
                    type="button"
                    key={item.id}
                    onClick={() => navigateTo(item.href)}
                  >
                    <span className="notification-item-topline">
                      <strong>{item.titulo}</strong>
                      <small className="notification-type">
                        {notificationTypeLabels[item.tipo] || item.tipo}
                      </small>
                    </span>
                    <span>{item.descricao}</span>
                    <small>{formatNotificationDate(item.data)} · {item.prioridade}</small>
                  </button>
                ))
              )}
            </div>
          )}
        </div>
        {isPlatformAdmin && (
          <div className="tenant-anchor">
            <button
              className={`tenant-switcher support-access-button${supportState.isActive ? ' support-mode' : ''}`}
              type="button"
              disabled={tenantSwitching}
              onClick={openSupportPanel}
            >
              <ShieldAlert size={16} />
              <span>{supportState.isActive ? (supportTenantName || activeTenant?.nomeExibicao || 'Suporte ativo') : 'Modo suporte'}</span>
              <ChevronDown size={16} />
            </button>
            {showTenants && (
              <div className="topbar-popover tenant-popover">
                <div className="popover-heading">
                  <div>
                    <strong>{supportState.isActive ? 'Modo suporte ativo' : 'Acesso operacional auditado'}</strong>
                    <small>
                      {supportState.isActive
                        ? 'Sessão temporária registrada para atendimento.'
                        : 'Abra uma sessão temporária para entrar nos dados do cliente.'}
                    </small>
                  </div>
                </div>

                {supportState.isActive ? (
                  <div className="support-panel">
                    <div className="support-panel-card">
                      <strong>{supportTenantName || activeTenant?.nomeExibicao || activeTenant?.nome || 'Cliente selecionado'}</strong>
                      <span>{supportReason}</span>
                      {supportState.expiresLabel && <small>Expira em {supportState.expiresLabel}</small>}
                    </div>
                    <button type="button" onClick={() => selectTenant(null)}>
                      <strong>Encerrar modo suporte</strong>
                      <span>Voltar para a empresa do seu login.</span>
                    </button>
                  </div>
                ) : (
                  <div className="support-panel">
                    {supportError && <div className="form-alert support-alert">{supportError}</div>}
                    <label className="form-field">
                      <span>Empresa</span>
                      <select value={supportTargetTenantId} onChange={(event) => setSupportTargetTenantId(event.target.value)}>
                        <option value="">Selecione uma empresa</option>
                        {tenantOptions.map((tenant) => (
                          <option key={tenant.id} value={tenant.id}>
                            {tenant.nomeExibicao || tenant.nome}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="form-field">
                      <span>Motivo</span>
                      <select value={supportCategory} onChange={(event) => setSupportCategory(event.target.value)}>
                        {supportReasonOptions.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="form-field">
                      <span>Contexto do atendimento</span>
                      <textarea
                        rows={3}
                        value={supportDetails}
                        placeholder="Descreva rapidamente o chamado, incidente ou validação solicitada."
                        onChange={(event) => setSupportDetails(event.target.value)}
                      />
                    </label>
                    <button type="button" onClick={startSupportForSelectedTenant}>
                      <strong>{tenantSwitching ? 'Abrindo suporte...' : 'Iniciar modo suporte'}</strong>
                      <span>O acesso ficará temporário, auditado e com expiração automática.</span>
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>
        )}
        <button
          className="user-menu"
          type="button"
          aria-label="Abrir configurações"
          title={supportState.isActive ? 'Encerrar o modo suporte para abrir configurações da plataforma' : 'Abrir configurações'}
          onClick={() => {
            if (supportState.isActive && isPlatformAdmin) {
              navigate('/');
              return;
            }

            navigate('/configuracoes');
          }}
        >
          <div className="user-chip" aria-label="Usuário atual">
            {initials}
          </div>
          <span>{usuario?.nome || 'Admin'}</span>
          <Settings size={16} />
        </button>
        <button className="logout-button" type="button" onClick={handleLogout}>
          Sair
        </button>
      </div>
    </header>
  );
}
