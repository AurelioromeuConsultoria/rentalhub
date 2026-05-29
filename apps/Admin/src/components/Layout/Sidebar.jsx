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

const menuGroups = [
  {
    title: 'Operação',
    items: [
      { label: 'Dashboard', href: '/', icon: BarChart3 },
      { label: 'Reservas', href: '/reservas', icon: CalendarDays },
      { label: 'Calendário', href: '/calendario', icon: ClipboardCheck },
      { label: 'Imóveis', href: '/imoveis', icon: Hotel },
      { label: 'Proprietários', href: '/proprietarios', icon: Users },
      { label: 'Hóspedes', href: '/hospedes', icon: UserRound },
    ],
  },
  {
    title: 'Gestão',
    items: [
      { label: 'Financeiro', href: '/financeiro', icon: Banknote },
      { label: 'Repasses', href: '/repasses', icon: ReceiptText },
      { label: 'Limpeza', href: '/limpeza', icon: Sparkles },
      { label: 'Manutenção', href: '/manutencao', icon: Wrench },
      { label: 'Relatórios', href: '/relatorios', icon: BarChart3 },
    ],
  },
  {
    title: 'Administração',
    items: [
      { label: 'Usuários', href: '/usuarios', icon: KeyRound },
      { label: 'Empresas', href: '/empresas', icon: Building2 },
      { label: 'Configurações', href: '/configuracoes', icon: Settings },
    ],
  },
];

export function Sidebar() {
  const [collapsed, setCollapsed] = useState(false);
  const [openGroups, setOpenGroups] = useState({
    Operação: true,
    Gestão: true,
    Administração: false,
  });

  const toggleGroup = (groupTitle) => {
    if (collapsed) {
      setCollapsed(false);
      setOpenGroups((current) => ({ ...current, [groupTitle]: true }));
      return;
    }

    setOpenGroups((current) => ({ ...current, [groupTitle]: !current[groupTitle] }));
  };

  return (
    <aside className={`sidebar${collapsed ? ' collapsed' : ''}`}>
      <div className="sidebar-logo-row">
        <div className="brand-block">
          <div className="brand-mark">
            <Home size={20} />
          </div>
          <div className="brand-copy">
            <strong>RentalHub</strong>
            <span>Admin</span>
          </div>
        </div>

        <button
          className="sidebar-collapse"
          type="button"
          aria-label={collapsed ? 'Expandir menu' : 'Recolher menu'}
          onClick={() => setCollapsed((current) => !current)}
        >
          {collapsed ? <PanelLeftOpen size={16} /> : <PanelLeftClose size={16} />}
        </button>

        <button className="mobile-menu-button" type="button" aria-label="Abrir menu">
          <Menu size={18} />
        </button>
      </div>

      <nav className="sidebar-nav" aria-label="Navegação principal">
        {menuGroups.map((group) => (
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
          <p>tenant: rentalhub</p>
        </div>
        <a href="https://malachdigital.com.br/" target="_blank" rel="noreferrer">
          <span className="malach-mark">M</span>
          <span>Desenvolvido por Malach Digital</span>
        </a>
      </div>
    </aside>
  );
}
