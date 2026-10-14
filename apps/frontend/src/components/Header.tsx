import { Link } from 'react-router-dom';
import { useAuth } from '../AuthContext';

export default function Header() {
  const { user, logout } = useAuth();

  return (
    <header className="app__header">
      <div className="app__header-content">
        <div>
          <Link to="/" className="app__title-link">
            <h1 className="app__title">
              Зачем тебе сохранять время на каком-то левом сайте?
            </h1>
          </Link>
          <p className="app__subtitle">Распределённое web-приложение</p>
        </div>

        <div className="app__header-actions">
          {user ? (
            <button onClick={logout} className="auth-button">
              Выйти
            </button>
          ) : (
            <Link to="/login" className="auth-button">
              Войти
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}