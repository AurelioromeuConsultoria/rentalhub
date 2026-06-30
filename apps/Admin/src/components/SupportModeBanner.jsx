import { ShieldAlert, ShieldX } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { supportAccessApi } from '@/api/supportAccess';
import { useAuth } from '@/context/AuthContext';
import {
  clearSupportAccessStorage,
  readSupportAccessState,
} from '@/lib/authStorage';

function formatDateTime(value) {
  if (!value) return '';

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';

  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(date);
}

export function SupportModeBanner() {
  const { usuario, currentTenant } = useAuth();
  const [state, setState] = useState(() => readSupportAccessState(currentTenant?.id));
  const [closing, setClosing] = useState(false);

  useEffect(() => {
    const sync = () => setState(readSupportAccessState(currentTenant?.id));
    sync();
    window.addEventListener('storage', sync);
    window.addEventListener('focus', sync);

    return () => {
      window.removeEventListener('storage', sync);
      window.removeEventListener('focus', sync);
    };
  }, [currentTenant?.id]);

  const expiresLabel = useMemo(() => formatDateTime(state.expiresAt), [state.expiresAt]);

  if (!usuario?.isPlatformAdmin || !state.isActive) {
    return null;
  }

  const endSupport = async () => {
    if (closing) return;

    setClosing(true);
    const token = state.token;
    clearSupportAccessStorage();
    setState(readSupportAccessState(currentTenant?.id));

    try {
      if (token) {
        await supportAccessApi.end(token);
      }
    } catch {
      // O estado local já foi limpo.
    } finally {
      window.location.assign('/');
    }
  };

  return (
    <section className="support-mode-banner">
      <div className="support-mode-banner-copy">
        <span className="support-mode-banner-kicker">
          <ShieldAlert size={15} />
          Modo suporte ativo
        </span>
        <strong>{state.tenantName || 'Cliente selecionado'}</strong>
        <span>{state.reason || 'Acesso temporário registrado para suporte operacional.'}</span>
      </div>
      <div className="support-mode-banner-meta">
        {expiresLabel && <small>Expira em {expiresLabel}</small>}
        <button type="button" onClick={endSupport} disabled={closing}>
          <ShieldX size={15} />
          {closing ? 'Encerrando...' : 'Encerrar suporte'}
        </button>
      </div>
    </section>
  );
}
