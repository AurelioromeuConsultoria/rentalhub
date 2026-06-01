/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useEffect, useMemo, useState } from 'react';
import { authApi } from '@/api/auth';
import {
  clearAuthStorage,
  clearLegacyAuthStorage,
  REFRESH_TOKEN_KEY,
  TOKEN_KEY,
  USER_KEY,
} from '@/lib/authStorage';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [usuario, setUsuario] = useState(() => {
    const savedUser = localStorage.getItem(USER_KEY);
    clearLegacyAuthStorage();
    if (!savedUser) return null;

    try {
      return JSON.parse(savedUser);
    } catch {
      clearAuthStorage();
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
        clearAuthStorage();
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
    clearAuthStorage();
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
