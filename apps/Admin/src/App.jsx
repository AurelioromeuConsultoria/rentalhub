import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from '@/components/Layout/Layout';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { AuthProvider } from '@/context/AuthContext';
import { ThemeProvider } from '@/context/ThemeContext';
import { CalendarioPage } from '@/pages/Calendario/CalendarioPage';
import { HospedesPage, ImoveisPage, ProprietariosPage } from '@/pages/Cadastros/CadastroPages';
import { Dashboard } from '@/pages/Dashboard/Dashboard';
import { FinanceiroPage } from '@/pages/Financeiro/FinanceiroPage';
import { LimpezaPage } from '@/pages/Limpeza/LimpezaPage';
import { Login } from '@/pages/Login/Login';
import { ManutencaoPage } from '@/pages/Manutencao/ManutencaoPage';
import { ModulePlaceholder } from '@/pages/Placeholder/ModulePlaceholder';
import { RelatoriosPage } from '@/pages/Relatorios/RelatoriosPage';
import { ReservasPage } from '@/pages/Reservas/ReservasPage';
import { RepassesPage } from '@/pages/Repasses/RepassesPage';

const moduleRoutes = [
  ['usuarios', 'Usuários', 'Controle de acessos e usuários do tenant.'],
  ['empresas', 'Empresas', 'Gestão de tenants da plataforma.'],
  ['configuracoes', 'Configurações', 'Preferências administrativas do RentalHub.'],
];

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
            <Route index element={<Dashboard />} />
            <Route path="reservas" element={<ReservasPage />} />
            <Route path="calendario" element={<CalendarioPage />} />
            <Route path="imoveis" element={<ImoveisPage />} />
            <Route path="proprietarios" element={<ProprietariosPage />} />
            <Route path="hospedes" element={<HospedesPage />} />
            <Route path="financeiro" element={<FinanceiroPage />} />
            <Route path="repasses" element={<RepassesPage />} />
            <Route path="limpeza" element={<LimpezaPage />} />
            <Route path="manutencao" element={<ManutencaoPage />} />
            <Route path="relatorios" element={<RelatoriosPage />} />
            {moduleRoutes.map(([path, title, description]) => (
              <Route
                key={path}
                path={path}
                element={<ModulePlaceholder title={title} description={description} />}
              />
            ))}
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </ThemeProvider>
  );
}
