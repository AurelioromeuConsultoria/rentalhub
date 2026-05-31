import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from '@/components/Layout/Layout';
import { ProtectedRoute } from '@/components/ProtectedRoute';
import { AuthProvider } from '@/context/AuthContext';
import { ThemeProvider } from '@/context/ThemeContext';
import { HospedesPage, ImoveisPage, ProprietariosPage } from '@/pages/Cadastros/CadastroPages';
import { Dashboard } from '@/pages/Dashboard/Dashboard';
import { Login } from '@/pages/Login/Login';
import { ModulePlaceholder } from '@/pages/Placeholder/ModulePlaceholder';

const moduleRoutes = [
  ['reservas', 'Reservas', 'Gestão de reservas, status, origem e valores.'],
  ['calendario', 'Calendário', 'Visão operacional de reservas, bloqueios e manutenções.'],
  ['financeiro', 'Financeiro', 'Receitas, despesas, categorias e fluxo de caixa.'],
  ['repasses', 'Repasses', 'Cálculo e controle de pagamentos aos proprietários.'],
  ['limpeza', 'Limpeza', 'Agenda operacional e status das limpezas.'],
  ['manutencao', 'Manutenção', 'Ocorrências, responsáveis e custos de manutenção.'],
  ['relatorios', 'Relatórios', 'Relatórios de reservas, financeiro, imóveis e proprietários.'],
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
            <Route path="imoveis" element={<ImoveisPage />} />
            <Route path="proprietarios" element={<ProprietariosPage />} />
            <Route path="hospedes" element={<HospedesPage />} />
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
