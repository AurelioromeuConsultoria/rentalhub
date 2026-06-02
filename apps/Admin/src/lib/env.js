export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? (import.meta.env.PROD ? window.location.origin : 'http://localhost:5015');
export const API_BASE_URL_WITH_API = `${API_BASE_URL}/api`;
