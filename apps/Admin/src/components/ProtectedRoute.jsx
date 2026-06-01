import { Navigate } from 'react-router-dom';
import { useAuth } from '@/context/AuthContext';

export function ProtectedRoute({ children, resource, access = 'view' }) {
  const { loading, isAuthenticated, hasPermission } = useAuth();

  if (loading) {
    return <div className="auth-loading">Carregando RentalHub...</div>;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (resource && !hasPermission(resource, access)) {
    return (
      <div className="access-denied">
        <strong>Acesso restrito</strong>
        <span>Seu perfil não possui permissão para este módulo.</span>
      </div>
    );
  }

  return children;
}
