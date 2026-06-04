import { Eye, KeyRound, Lock } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { authApi } from '@/api/auth';
import { RentalHubMark } from '@/components/Brand/RentalHubMark';

export function SetPassword() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = useMemo(() => searchParams.get('token') || '', [searchParams]);
  const [senha, setSenha] = useState('');
  const [confirmacao, setConfirmacao] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const handleSubmit = async (event) => {
    event.preventDefault();
    setError('');
    setSuccess('');

    if (!token) {
      setError('Link inválido ou incompleto.');
      return;
    }

    if (senha.length < 8) {
      setError('A senha deve ter pelo menos 8 caracteres.');
      return;
    }

    if (senha !== confirmacao) {
      setError('As senhas não conferem.');
      return;
    }

    setSubmitting(true);
    try {
      const response = await authApi.setPassword({ token, senha });
      setSuccess(response.data?.message || 'Senha definida com sucesso.');
      setTimeout(() => navigate('/login'), 1000);
    } catch (setPasswordError) {
      setError(setPasswordError.response?.data?.message || 'Não foi possível definir sua senha.');
    } finally {
      setSubmitting(false);
    }
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
          <h1>Defina sua senha com segurança.</h1>
          <p>Use o link recebido por convite ou redefinição para criar uma nova senha de acesso.</p>
        </div>

        <div className="login-feature-row" aria-hidden="true">
          <span>Convite</span>
          <span>Acesso</span>
          <span>Segurança</span>
          <span>Equipe</span>
        </div>
      </section>

      <section className="login-panel" aria-label="Definir senha">
        <div>
          <span className="eyebrow">Acesso seguro</span>
          <h2>Definir senha</h2>
        </div>

        <form className="login-form" onSubmit={handleSubmit}>
          <label>
            Nova senha
            <span className="field-with-icon">
              <Lock size={18} />
              <input
                type={showPassword ? 'text' : 'password'}
                placeholder="Mínimo 8 caracteres"
                value={senha}
                onChange={(event) => setSenha(event.target.value)}
                autoComplete="new-password"
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
          <label>
            Confirmar senha
            <span className="field-with-icon">
              <Lock size={18} />
              <input
                type={showPassword ? 'text' : 'password'}
                placeholder="Repita a senha"
                value={confirmacao}
                onChange={(event) => setConfirmacao(event.target.value)}
                autoComplete="new-password"
                required
              />
            </span>
          </label>
          {error && <div className="login-error">{error}</div>}
          {success && <div className="login-success">{success}</div>}
          <button className="primary-action full" type="submit" disabled={submitting}>
            <KeyRound size={18} />
            {submitting ? 'Salvando...' : 'Definir senha'}
          </button>
          <Link className="text-action" to="/login">Voltar para login</Link>
        </form>
      </section>
    </main>
  );
}
