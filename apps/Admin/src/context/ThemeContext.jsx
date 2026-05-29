/* eslint-disable react-refresh/only-export-components */
import { createContext, useContext, useEffect, useMemo, useState } from 'react';

const ThemeContext = createContext(null);
const THEME_KEY = 'rentalhub-theme';
const LIGHT = 'light';
const DARK = 'dark';

function getInitialTheme() {
  const savedTheme = localStorage.getItem(THEME_KEY);
  if (savedTheme === LIGHT || savedTheme === DARK) {
    return savedTheme;
  }

  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? DARK : LIGHT;
}

export function ThemeProvider({ children }) {
  const [theme, setTheme] = useState(getInitialTheme);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem(THEME_KEY, theme);
  }, [theme]);

  const value = useMemo(
    () => ({
      theme,
      isDark: theme === DARK,
      toggleTheme: () => setTheme((current) => (current === DARK ? LIGHT : DARK)),
    }),
    [theme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme deve ser usado dentro de ThemeProvider');
  }

  return context;
}
