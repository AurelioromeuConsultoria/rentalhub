import { X } from 'lucide-react';
import { useEffect, useState } from 'react';
import { sistemaApi } from '@/api/sistema';
import { APP_VERSION } from '@/lib/version';

const DISMISSED_UPDATE_KEY = 'rentalhub-dismissed-update-version';

export function SystemStatusBanner() {
  const [status, setStatus] = useState(null);
  const [dismissedVersion, setDismissedVersion] = useState(() => localStorage.getItem(DISMISSED_UPDATE_KEY));

  useEffect(() => {
    const timeout = setTimeout(async () => {
      try {
        const response = await sistemaApi.status();
        setStatus(response.data);
      } catch {
        setStatus(null);
      }
    }, 0);

    return () => clearTimeout(timeout);
  }, []);

  const notice = status?.avisoAtualizacao;
  const noticeVersion = notice?.versao || status?.adminVersion || APP_VERSION;
  const shouldShow = notice?.ativo && dismissedVersion !== noticeVersion;

  if (!shouldShow) {
    return null;
  }

  const dismiss = () => {
    localStorage.setItem(DISMISSED_UPDATE_KEY, noticeVersion);
    setDismissedVersion(noticeVersion);
  };

  return (
    <section className="system-update-banner">
      <div>
        <strong>{notice.titulo || `RentalHub atualizado para v${noticeVersion}`}</strong>
        <span>{notice.mensagem || 'Nova versão disponível com melhorias de estabilidade e operação.'}</span>
      </div>
      <small>v{noticeVersion}</small>
      <button type="button" aria-label="Dispensar aviso" onClick={dismiss}>
        <X size={16} />
      </button>
    </section>
  );
}
