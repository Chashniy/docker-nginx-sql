import { useState, FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../AuthContext';

export default function LoginPage() {
  const { login, register, user } = useAuth();
  const navigate = useNavigate();

  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  // Если уже залогинен — редирект на /admin
  if (user) {
    navigate('/admin', { replace: true });
    return null;
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      if (mode === 'login') {
        await login(username, password);
      } else {
        await register(username, password);
      }
      navigate('/admin', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ошибка');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-form">
      <h2 className="login-form__title">
        {mode === 'login' ? 'Вход' : 'Регистрация'}
      </h2>

      <form onSubmit={handleSubmit} className="login-form__form">
        <label className="login-form__label">
          Логин
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            className="login-form__input"
            required
            minLength={3}
            autoComplete="username"
          />
        </label>

        <label className="login-form__label">
          Пароль
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="login-form__input"
            required
            minLength={6}
            autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
          />
        </label>

        {error && <div className="login-form__error">{error}</div>}

        <button type="submit" className="btn" disabled={loading}>
          {loading ? '...' : mode === 'login' ? 'Войти' : 'Зарегистрироваться'}
        </button>
      </form>

      <button
        type="button"
        onClick={() => {
          setMode(mode === 'login' ? 'register' : 'login');
          setError(null);
        }}
        className="login-form__toggle"
      >
        {mode === 'login' ? 'Нет аккаунта? Зарегистрироваться' : 'Уже есть аккаунт? Войти'}
      </button>
    </div>
  );
}