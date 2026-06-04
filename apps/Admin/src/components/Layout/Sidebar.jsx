import {
  Banknote,
  BarChart3,
  Building2,
  CalendarDays,
  ChevronDown,
  ChevronRight,
  ClipboardCheck,
  Home,
  Hotel,
  History,
  LifeBuoy,
  KeyRound,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  ReceiptText,
  Settings,
  Shield,
  Sparkles,
  UserRound,
  Users,
  Wrench,
} from 'lucide-react';
import { useState } from 'react';
import { NavLink } from 'react-router-dom';
import { RentalHubMark } from '@/components/Brand/RentalHubMark';
import { useAuth } from '@/context/AuthContext';
import { APP_VERSION } from '@/lib/version';

const SIDEBAR_COLLAPSED_KEY = 'rentalhub-sidebar-collapsed';

function getInitialCollapsed() {
  try {
    return localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === 'true';
  } catch {
    return false;
  }
}

const menuGroups = [
  {
    title: 'Operação',
    items: [
      { label: 'Dashboard', href: '/', icon: BarChart3, resource: 'dashboard' },
      { label: 'Reservas', href: '/reservas', icon: CalendarDays, resource: 'reservas' },
      { label: 'Calendário', href: '/calendario', icon: ClipboardCheck, resource: 'calendario' },
      { label: 'Imóveis', href: '/imoveis', icon: Hotel, resource: 'imoveis' },
      { label: 'Proprietários', href: '/proprietarios', icon: Users, resource: 'proprietarios' },
      { label: 'Hóspedes', href: '/hospedes', icon: UserRound, resource: 'hospedes' },
    ],
  },
  {
    title: 'Gestão',
    items: [
      { label: 'Financeiro', href: '/financeiro', icon: Banknote, resource: 'financeiro' },
      { label: 'Repasses', href: '/repasses', icon: ReceiptText, resource: 'repasses' },
      { label: 'Limpeza', href: '/limpeza', icon: Sparkles, resource: 'limpezas' },
      { label: 'Manutenção', href: '/manutencao', icon: Wrench, resource: 'manutencoes' },
      { label: 'Relatórios', href: '/relatorios', icon: BarChart3, resource: 'relatorios' },
    ],
  },
  {
    title: 'Administração',
    items: [
      { label: 'Usuários', href: '/usuarios', icon: KeyRound, resource: 'usuarios' },
      { label: 'Perfis', href: '/perfis', icon: Shield, resource: 'perfis-acesso' },
      { label: 'Empresas', href: '/empresas', icon: Building2, resource: 'tenants' },
      { label: 'Configurações', href: '/configuracoes', icon: Settings, resource: 'configuracoes' },
      { label: 'Auditoria', href: '/auditoria', icon: History, resource: 'auditoria' },
    ],
  },
  {
    title: 'Atendimento',
    items: [
      { label: 'Suporte', href: '/suporte', icon: LifeBuoy },
    ],
  },
];

export function Sidebar() {
  const { usuario, canView } = useAuth();
  const isOwner = Number(usuario?.tipoUsuario) === 4;
  const isPlatformAdmin = Boolean(usuario?.isPlatformAdmin);
  const [collapsed, setCollapsed] = useState(getInitialCollapsed);
  const [openGroups, setOpenGroups] = useState({
    Operação: true,
    Gestão: true,
    Administração: false,
  });
  const visibleGroups = isOwner
    ? [
        {
          title: 'Portal',
          items: [{ label: 'Meu portal', href: '/portal-proprietario', icon: Home }],
        },
      ]
    : menuGroups
        .map((group) => ({
          ...group,
          items: group.items.filter((item) => {
            if (item.href === '/empresas') {
              return isPlatformAdmin;
            }

            return !item.resource || canView(item.resource);
          }),
        }))
        .filter((group) => group.items.length > 0);

  const toggleGroup = (groupTitle) => {
    if (collapsed) {
      setCollapsed(false);
      setOpenGroups((current) => ({ ...current, [groupTitle]: true }));
      return;
    }

    setOpenGroups((current) => ({ ...current, [groupTitle]: !current[groupTitle] }));
  };

  const toggleCollapsed = () => {
    setCollapsed((current) => {
      const next = !current;
      try {
        localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(next));
      } catch {
        // O estado visual ainda funciona mesmo sem persistência local.
      }
      return next;
    });
  };

  return (
    <aside className={`sidebar${collapsed ? ' collapsed' : ''}`}>
      <div className="sidebar-logo-row">
        <div className="brand-block">
          <div className="brand-mark">
            <RentalHubMark />
          </div>
          <div className="brand-copy">
            <strong>RentalHub</strong>
            <span>{isOwner ? 'Proprietário' : 'Admin'}</span>
          </div>
        </div>

        <button
          className="sidebar-collapse"
          type="button"
          aria-label={collapsed ? 'Expandir menu' : 'Recolher menu'}
          onClick={toggleCollapsed}
        >
          {collapsed ? <PanelLeftOpen size={16} /> : <PanelLeftClose size={16} />}
        </button>

        <button className="mobile-menu-button" type="button" aria-label="Abrir menu">
          <Menu size={18} />
        </button>
      </div>

      <nav className="sidebar-nav" aria-label="Navegação principal">
        {visibleGroups.map((group) => (
          <section className="nav-group" key={group.title}>
            <button
              className="nav-group-trigger"
              type="button"
              title={group.title}
              onClick={() => toggleGroup(group.title)}
            >
              <span>{group.title}</span>
              {openGroups[group.title] ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
            </button>
            <div className={`nav-group-content${openGroups[group.title] && !collapsed ? ' open' : ''}`}>
              {group.items.map((item) => {
                const Icon = item.icon;

                return (
                  <NavLink
                    className={({ isActive }) => `nav-link${isActive ? ' active' : ''}`}
                    end={item.href === '/'}
                    key={item.href}
                    title={item.label}
                    to={item.href}
                  >
                    <Icon size={18} />
                    <span>{item.label}</span>
                  </NavLink>
                );
              })}
            </div>
          </section>
        ))}
      </nav>

      <div className="sidebar-footer">
        <div className="platform-card">
          <div>
            <Shield size={16} />
            <strong>Modo plataforma</strong>
          </div>
          <p>{isOwner ? 'Portal do proprietário' : isPlatformAdmin ? 'Admin geral' : 'Empresa ativa'}</p>
        </div>
        <span className="app-version">v{APP_VERSION}</span>
        <a href="https://malachdigital.com.br/" target="_blank" rel="noreferrer">
          <span className="malach-mark">M</span>
          <span>Desenvolvido por Malach Digital</span>
        </a>
      </div>
    </aside>
  );
}
