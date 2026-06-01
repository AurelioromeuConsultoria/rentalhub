import { Bell, ChevronDown, Globe, Moon, Search, Settings, Sun } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { buscaGlobalApi, notificacoesApi } from '@/api/operacao';
import { useAuth } from '@/context/AuthContext';
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
  empresas: 'Empresas',
  configuracoes: 'Configurações',
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

export function Header() {
  const location = useLocation();
  const navigate = useNavigate();
  const { usuario, currentTenant, logout } = useAuth();
  const { isDark, toggleTheme } = useTheme();
  const [notifications, setNotifications] = useState([]);
  const [showNotifications, setShowNotifications] = useState(false);
  const [search, setSearch] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [showSearch, setShowSearch] = useState(false);
  const breadcrumbs = getBreadcrumbs(location.pathname);
  const highPriorityCount = useMemo(
    () => notifications.filter((item) => item.prioridade === 'alta').length,
    [notifications],
  );
  const initials = usuario?.nome
    ?.split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase() || 'RH';

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const loadNotifications = useCallback(async () => {
    try {
      const response = await notificacoesApi.list({ dias: 3 });
      setNotifications(response.data || []);
    } catch {
      setNotifications([]);
    }
  }, []);

  useEffect(() => {
    const timeout = setTimeout(loadNotifications, 0);
    return () => clearTimeout(timeout);
  }, [loadNotifications, location.pathname]);

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
                <span className="popover-empty">Nada encontrado</span>
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
        <button className="icon-button" type="button" aria-label="Idioma">
          <Globe size={18} />
        </button>
        <button
          className="icon-button"
          type="button"
          aria-label={isDark ? 'Ativar tema claro' : 'Ativar tema escuro'}
          title={isDark ? 'Tema claro' : 'Tema escuro'}
          onClick={toggleTheme}
        >
          {isDark ? <Sun size={18} /> : <Moon size={18} />}
        </button>
        <div className="notification-anchor">
          <button
            className="icon-button notification-button"
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
                <strong>Notificações</strong>
                <button type="button" onClick={loadNotifications}>Atualizar</button>
              </div>
              {notifications.length === 0 ? (
                <span className="popover-empty">Nenhuma pendência crítica</span>
              ) : (
                notifications.slice(0, 8).map((item) => (
                  <button type="button" key={item.id} onClick={() => navigateTo(item.href)}>
                    <strong>{item.titulo}</strong>
                    <span>{item.descricao}</span>
                    <small>{formatDate(item.data)} · {item.prioridade}</small>
                  </button>
                ))
              )}
            </div>
          )}
        </div>
        <button className="tenant-switcher" type="button">
          <span className="tenant-dot" />
          <span>{currentTenant?.nomeExibicao || currentTenant?.nome || 'RentalHub'}</span>
          <ChevronDown size={16} />
        </button>
        <button className="user-menu" type="button">
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
