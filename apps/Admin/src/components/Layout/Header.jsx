import { Bell, ChevronDown, Globe, Moon, Settings, Sun } from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
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

export function Header() {
  const location = useLocation();
  const navigate = useNavigate();
  const { usuario, currentTenant, logout } = useAuth();
  const { isDark, toggleTheme } = useTheme();
  const breadcrumbs = getBreadcrumbs(location.pathname);
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
        <button className="icon-button" type="button" aria-label="Notificações">
          <Bell size={18} />
        </button>
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
