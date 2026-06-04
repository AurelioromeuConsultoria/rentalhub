import { Eye, KeyRound, Lock, Mail } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { authApi } from '@/api/auth';
import { RentalHubMark } from '@/components/Brand/RentalHubMark';
import { useAuth } from '@/context/AuthContext';

export function Login() {
  const navigate = useNavigate();
  const { login, isAuthenticated } = useAuth();
  const [email, setEmail] = useState('admin@rentalhub.com');
  const [senha, setSenha] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [forgotMode, setForgotMode] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/');
    }
  }, [isAuthenticated, navigate]);

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');
    setSuccess('');
    setSubmitting(true);

    if (forgotMode) {
      try {
        const response = await authApi.forgotPassword({ email });
        setSuccess(response.data?.message || 'Se o e-mail estiver cadastrado, enviaremos as instruções.');
        if (response.data?.url) {
          setSuccess(`${response.data.message} Link: ${response.data.url}`);
        }
      } catch (forgotError) {
        setError(forgotError.response?.data?.message || 'Não foi possível solicitar redefinição de senha.');
      } finally {
        setSubmitting(false);
      }
      return;
    }

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
            <RentalHubMark />
          </div>
          <div>
            <strong>RentalHub</strong>
            <span>Operação de locações</span>
          </div>
        </div>

        <div className="login-copy">
          <h1>Operação de temporada em um painel só.</h1>
          <p>
            Reservas, imóveis, hóspedes, financeiro e repasses organizados para cada empresa.
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
          <h2>{forgotMode ? 'Recuperar senha' : 'Entrar no RentalHub'}</h2>
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
          {!forgotMode && (
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
          )}
          {error && <div className="login-error">{error}</div>}
          {success && <div className="login-success">{success}</div>}
          <button className="primary-action full" type="submit" disabled={submitting}>
            <KeyRound size={18} />
            {submitting ? 'Aguarde...' : forgotMode ? 'Enviar instruções' : 'Entrar'}
          </button>
          <button
            className="text-action"
            type="button"
            onClick={() => {
              setForgotMode((current) => !current);
              setError('');
              setSuccess('');
            }}
          >
            {forgotMode ? 'Voltar para login' : 'Esqueci minha senha'}
          </button>
          <div className="login-legal-links">
            <Link to="/termos-de-uso">Termos de Uso</Link>
            <Link to="/privacidade">Privacidade</Link>
          </div>
        </form>
      </section>
    </main>
  );
}
