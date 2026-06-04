import { Bell, ChevronDown, Moon, RefreshCw, Search, Settings, Sun } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { tenantsApi } from '@/api/administracao';
import { buscaGlobalApi, notificacoesApi } from '@/api/operacao';
import { useAuth } from '@/context/AuthContext';
import { SELECTED_TENANT_ID_KEY, SELECTED_TENANT_SLUG_KEY } from '@/lib/authStorage';
import { TENANTS_UPDATED_EVENT } from '@/lib/tenantEvents';
import { useTheme } from '@/context/ThemeContext';

const routeLabels = {
  '': 'Dashboard',
  reservas: 'Reservas',
  calendario: 'Calendário',
  imoveis: 'Imóveis',
  proprietarios: 'Proprietários',
  hospedes: 'Hóspedes',
  financeiro: 'Financeiro',
  repasses: 'Repasses',
  limpeza: 'Limpeza',
  manutencao: 'Manutenção',
  relatorios: 'Relatórios',
  'portal-proprietario': 'Portal do Proprietário',
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
    if (!usuario?.isPlatformAdmin) {
      setTenants([]);
      return;
    }

    try {
      const response = await tenantsApi.list();
      setTenants(response.data || []);
    } catch {
      setTenants([]);
    }
  }, [usuario?.isPlatformAdmin]);

  useEffect(() => {
    const timeout = setTimeout(loadTenants, 0);
    window.addEventListener(TENANTS_UPDATED_EVENT, loadTenants);

    return () => {
      clearTimeout(timeout);
      window.removeEventListener(TENANTS_UPDATED_EVENT, loadTenants);
    };
  }, [loadTenants]);

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

  const selectTenant = (tenant) => {
    if (!tenant) {
      localStorage.removeItem(SELECTED_TENANT_ID_KEY);
      localStorage.removeItem(SELECTED_TENANT_SLUG_KEY);
      setSelectedTenantId(null);
    } else {
      localStorage.setItem(SELECTED_TENANT_ID_KEY, String(tenant.id));
      localStorage.setItem(SELECTED_TENANT_SLUG_KEY, tenant.slug);
      setSelectedTenantId(String(tenant.id));
    }

    setShowTenants(false);
    navigate('/');
    window.location.reload();
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
        <div className="tenant-anchor">
          <button
            className="tenant-switcher"
            type="button"
            onClick={() => {
              if (!usuario?.isPlatformAdmin) {
                return;
              }

              setShowTenants((current) => !current);
              loadTenants();
            }}
          >
            <span className="tenant-dot" />
            <span>{activeTenant?.nomeExibicao || activeTenant?.nome || 'RentalHub'}</span>
            <ChevronDown size={16} />
          </button>
          {usuario?.isPlatformAdmin && showTenants && (
            <div className="topbar-popover tenant-popover">
              <div className="popover-heading">
                <strong>Operar empresa</strong>
              </div>
              <button type="button" onClick={() => selectTenant(null)}>
                <strong>{currentTenant?.nomeExibicao || currentTenant?.nome || 'Minha empresa'}</strong>
                <span>Empresa do meu login</span>
              </button>
              {tenantOptions.map((tenant) => (
                <button type="button" key={tenant.id} onClick={() => selectTenant(tenant)}>
                  <strong>{tenant.nomeExibicao}</strong>
                  <span>{tenant.ativo ? 'Empresa ativa' : 'Empresa inativa'}</span>
                </button>
              ))}
            </div>
          )}
        </div>
        <button
          className="user-menu"
          type="button"
          aria-label="Abrir configurações"
          title="Abrir configurações"
          onClick={() => navigate('/configuracoes')}
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
