/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { authApi } from '@/api/auth';

const AuthContext = createContext(null);
const TOKEN_KEY = 'token';
const REFRESH_TOKEN_KEY = 'refreshToken';
const USER_KEY = 'usuario';

function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

export function AuthProvider({ children }) {
  const [usuario, setUsuario] = useState(() => {
    const savedUser = localStorage.getItem(USER_KEY);
    if (!savedUser) return null;

    try {
      return JSON.parse(savedUser);
    } catch {
      clearSession();
      return null;
    }
  });
  const [loading, setLoading] = useState(() => !!localStorage.getItem(TOKEN_KEY));

  useEffect(() => {
    const token = localStorage.getItem(TOKEN_KEY);

    if (!token) {
      return;
    }

    authApi
      .me()
      .then((response) => {
        setUsuario(response.data);
        localStorage.setItem(USER_KEY, JSON.stringify(response.data));
      })
      .catch(() => {
        clearSession();
        setUsuario(null);
      })
      .finally(() => setLoading(false));
  }, []);

  const login = async (email, senha) => {
    try {
      const response = await authApi.login({ email, senha });
      const { token, refreshToken, usuario: usuarioData } = response.data;

      localStorage.setItem(TOKEN_KEY, token);
      localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
      localStorage.setItem(USER_KEY, JSON.stringify(usuarioData));
      setUsuario(usuarioData);

      return { success: true };
    } catch (error) {
      return {
        success: false,
        message:
          error.response?.data?.message ||
          error.response?.data?.error ||
          'Email ou senha inválidos',
      };
    }
  };

  const logout = () => {
    clearSession();
    setUsuario(null);
  };

  const value = useMemo(
    () => ({
      usuario,
      loading,
      login,
      logout,
      isAuthenticated: !!usuario,
      currentTenant: usuario
        ? {
            id: usuario.tenantId,
            slug: usuario.tenantSlug,
            nome: usuario.tenantNome,
            nomeExibicao: usuario.tenantNomeExibicao,
            isRootTenant: usuario.isRootTenant,
          }
        : null,
    }),
    [usuario, loading],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth deve ser usado dentro de AuthProvider');
  }
  return context;
}
