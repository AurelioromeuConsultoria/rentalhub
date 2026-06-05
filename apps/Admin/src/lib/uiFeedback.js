export function getFriendlyErrorMessage(error, fallback = 'Não foi possível concluir a operação.') {
  const message = error?.response?.data?.message || error?.response?.data?.error || '';
  const status = error?.response?.status;

  if (status === 401) {
    return 'Sua sessão expirou. Faça login novamente para continuar.';
  }

  if (status === 403) {
    return 'Você não tem permissão para executar esta ação.';
  }

  if (status === 404) {
    return 'Não encontramos esse registro. Atualize a tela e tente novamente.';
  }

  if (status === 409) {
    return message || 'Já existe um conflito com os dados informados. Revise o período, vínculo ou cadastro selecionado.';
  }

  if (status >= 500) {
    return 'O servidor não conseguiu concluir a operação agora. Tente novamente em alguns segundos.';
  }

  return message || fallback;
}

export function confirmAction(title, description) {
  return window.confirm(`${title}\n\n${description}`);
}
