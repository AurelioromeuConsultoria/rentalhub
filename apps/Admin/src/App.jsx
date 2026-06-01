import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from '@/components/Layout/Layout';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { AuthProvider } from '@/context/AuthContext';
import { ThemeProvider } from '@/context/ThemeContext';
import { AuditoriaPage } from '@/pages/Auditoria/AuditoriaPage';
import { CalendarioPage } from '@/pages/Calendario/CalendarioPage';
import { HospedesPage, ImoveisPage, ProprietariosPage } from '@/pages/Cadastros/CadastroPages';
import { Dashboard } from '@/pages/Dashboard/Dashboard';
import { FinanceiroPage } from '@/pages/Financeiro/FinanceiroPage';
import { LimpezaPage } from '@/pages/Limpeza/LimpezaPage';
import { Login } from '@/pages/Login/Login';
import { ManutencaoPage } from '@/pages/Manutencao/ManutencaoPage';
import { PortalProprietarioPage } from '@/pages/PortalProprietario/PortalProprietarioPage';
import { RelatoriosPage } from '@/pages/Relatorios/RelatoriosPage';
import { ReservasPage } from '@/pages/Reservas/ReservasPage';
import { RepassesPage } from '@/pages/Repasses/RepassesPage';
import { useAuth } from '@/context/AuthContext';
import { ConfiguracoesPage, EmpresasPage, UsuariosPage } from '@/pages/Administracao/AdministracaoPages';

const internalRoutes = [
  { path: '/reservas', resource: 'reservas' },
  { path: '/calendario', resource: 'calendario' },
  { path: '/imoveis', resource: 'imoveis' },
  { path: '/proprietarios', resource: 'proprietarios' },
  { path: '/hospedes', resource: 'hospedes' },
  { path: '/financeiro', resource: 'financeiro' },
  { path: '/repasses', resource: 'repasses' },
  { path: '/limpeza', resource: 'limpezas' },
  { path: '/manutencao', resource: 'manutencoes' },
  { path: '/relatorios', resource: 'relatorios' },
  { path: '/usuarios', resource: 'usuarios' },
  { path: '/empresas', resource: 'tenants' },
  { path: '/configuracoes', resource: 'tenants' },
  { path: '/auditoria', resource: 'auditoria' },
];

function HomeRoute() {
  const { usuario, canView } = useAuth();
  if (Number(usuario?.tipoUsuario) === 4) {
    return <Navigate to="/portal-proprietario" replace />;
  }

  if (canView('dashboard')) {
    return <Dashboard />;
  }

  const firstAllowedRoute = internalRoutes.find((route) => canView(route.resource));
  if (firstAllowedRoute) {
    return <Navigate to={firstAllowedRoute.path} replace />;
  }

  return (
    <div className="access-denied">
      <strong>Acesso restrito</strong>
      <span>Seu perfil ainda não possui módulos liberados.</span>
    </div>
  );
}

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route
            path="/"
            element={
              <ProtectedRoute>
                <Layout />
              </ProtectedRoute>
            }
          >
            <Route index element={<HomeRoute />} />
            <Route path="reservas" element={<ProtectedRoute resource="reservas"><ReservasPage /></ProtectedRoute>} />
            <Route path="calendario" element={<ProtectedRoute resource="calendario"><CalendarioPage /></ProtectedRoute>} />
            <Route path="imoveis" element={<ProtectedRoute resource="imoveis"><ImoveisPage /></ProtectedRoute>} />
            <Route path="proprietarios" element={<ProtectedRoute resource="proprietarios"><ProprietariosPage /></ProtectedRoute>} />
            <Route path="hospedes" element={<ProtectedRoute resource="hospedes"><HospedesPage /></ProtectedRoute>} />
            <Route path="financeiro" element={<ProtectedRoute resource="financeiro"><FinanceiroPage /></ProtectedRoute>} />
            <Route path="repasses" element={<ProtectedRoute resource="repasses"><RepassesPage /></ProtectedRoute>} />
            <Route path="limpeza" element={<ProtectedRoute resource="limpezas"><LimpezaPage /></ProtectedRoute>} />
            <Route path="manutencao" element={<ProtectedRoute resource="manutencoes"><ManutencaoPage /></ProtectedRoute>} />
            <Route path="relatorios" element={<ProtectedRoute resource="relatorios"><RelatoriosPage /></ProtectedRoute>} />
            <Route path="portal-proprietario" element={<PortalProprietarioPage />} />
            <Route path="usuarios" element={<ProtectedRoute resource="usuarios"><UsuariosPage /></ProtectedRoute>} />
            <Route path="empresas" element={<ProtectedRoute resource="tenants"><EmpresasPage /></ProtectedRoute>} />
            <Route path="configuracoes" element={<ProtectedRoute resource="tenants"><ConfiguracoesPage /></ProtectedRoute>} />
            <Route path="auditoria" element={<ProtectedRoute resource="auditoria"><AuditoriaPage /></ProtectedRoute>} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </ThemeProvider>
  );
}
