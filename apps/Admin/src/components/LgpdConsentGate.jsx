import { ShieldCheck } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { lgpdApi } from '@/api/lgpd';
import { useAuth } from '@/context/AuthContext';

export function LgpdConsentGate() {
  const { isAuthenticated } = useAuth();
  const [status, setStatus] = useState(null);
  const [checked, setChecked] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let mounted = true;

    if (!isAuthenticated) {
      const timeout = setTimeout(() => {
        if (mounted) {
          setStatus(null);
        }
      }, 0);

      return () => {
        mounted = false;
        clearTimeout(timeout);
      };
    }

    lgpdApi
      .status()
      .then((response) => {
        if (mounted) {
          setStatus(response.data);
        }
      })
      .catch(() => {
        if (mounted) {
          setStatus({ accepted: true });
        }
      });

    return () => {
      mounted = false;
    };
  }, [isAuthenticated]);

  if (!isAuthenticated || !status || status.accepted) {
    return null;
  }

  const accept = async () => {
    setSaving(true);
    setError('');
    try {
      const response = await lgpdApi.accept({
        termsVersion: status.termsVersion,
        privacyVersion: status.privacyVersion,
      });
      setStatus(response.data);
    } catch (acceptError) {
      setError(acceptError.response?.data?.message || 'Não foi possível registrar o aceite.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="consent-backdrop" role="dialog" aria-modal="true" aria-labelledby="lgpd-title">
      <section className="consent-modal">
        <div className="consent-icon">
          <ShieldCheck size={28} />
        </div>
        <h2 id="lgpd-title">Aceite de termos e privacidade</h2>
        <p>
          Para continuar usando o RentalHub, confirme a ciência dos Termos de Uso e da Política de
          Privacidade vigentes.
        </p>
        <div className="consent-links">
          <Link to="/termos-de-uso" target="_blank" rel="noreferrer">Termos de Uso</Link>
          <Link to="/privacidade" target="_blank" rel="noreferrer">Política de Privacidade</Link>
        </div>
        <label className="checkbox-row">
          <input type="checkbox" checked={checked} onChange={(event) => setChecked(event.target.checked)} />
          Li e aceito os Termos de Uso e a Política de Privacidade.
        </label>
        {error && <div className="form-alert">{error}</div>}
        <button className="primary-action full" type="button" disabled={!checked || saving} onClick={accept}>
          {saving ? 'Registrando...' : 'Aceitar e continuar'}
        </button>
      </section>
    </div>
  );
}
