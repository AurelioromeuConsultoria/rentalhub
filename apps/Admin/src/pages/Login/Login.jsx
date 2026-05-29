import { Building2, Eye, KeyRound, Lock, Mail } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '@/context/AuthContext';

export function Login() {
  const navigate = useNavigate();
  const { login, isAuthenticated } = useAuth();
  const [email, setEmail] = useState('admin@rentalhub.com');
  const [senha, setSenha] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/');
    }
  }, [isAuthenticated, navigate]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');
    setSubmitting(true);

    const result = await login(email, senha);
    setSubmitting(false);

    if (result.success) {
      navigate('/');
      return;
    }

    setError(result.message);
    setSenha('');
  };

  return (
    <main className="login-page">
      <section className="login-visual">
        <div className="brand-block login-brand">
          <div className="brand-mark">
            <Building2 size={22} />
          </div>
          <div>
            <strong>RentalHub</strong>
            <span>Operação de locações</span>
          </div>
        </div>

        <div className="login-copy">
          <h1>Gestão de temporada em um painel só.</h1>
          <p>
            Reservas, imóveis, hóspedes, financeiro e repasses com isolamento multi-tenant desde a fundação.
          </p>
        </div>

        <div className="login-feature-row" aria-hidden="true">
          <span>Imóveis</span>
          <span>Reservas</span>
          <span>Repasses</span>
          <span>Operação</span>
        </div>
      </section>

      <section className="login-panel" aria-label="Login administrativo">
        <div>
          <span className="eyebrow">Acesso administrativo</span>
          <h2>Entrar no RentalHub</h2>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <label>
            E-mail
            <span className="field-with-icon">
              <Mail size={18} />
              <input
                type="email"
                placeholder="admin@rentalhub.com"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                autoComplete="email"
                required
              />
            </span>
          </label>
          <label>
            Senha
            <span className="field-with-icon">
              <Lock size={18} />
              <input
                type={showPassword ? 'text' : 'password'}
                placeholder="Sua senha"
                value={senha}
                onChange={(event) => setSenha(event.target.value)}
                autoComplete="current-password"
                required
              />
              <button
                className="password-toggle"
                type="button"
                aria-label={showPassword ? 'Ocultar senha' : 'Mostrar senha'}
                onClick={() => setShowPassword((current) => !current)}
              >
                <Eye size={18} />
              </button>
            </span>
          </label>
          {error && <div className="login-error">{error}</div>}
          <button className="primary-action full" type="submit" disabled={submitting}>
            <KeyRound size={18} />
            {submitting ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </section>
    </main>
  );
}
