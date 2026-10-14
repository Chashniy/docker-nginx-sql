import { Routes, Route, Link, useLocation } from 'react-router-dom';
import { useAuth } from './AuthContext';
import ProtectedRoute from './components/ProtectedRoute';
import HomePage from './pages/HomePage';
import LoginPage from './pages/LoginPage';
import AdminPage from './pages/AdminPage';

export default function App() {
  const { user, logout } = useAuth();
  const location = useLocation();

  // На странице логина не показываем кнопки авторизации
  const isLoginPage = location.pathname === '/login';

  return (
    <div className="app">
      <header className="app__header">
        <div className="app__header-content">
          <div>
            <Link to="/" className="app__title-link">
              <h1 className="app__title">Зачем тебе сохранять время на каком-то левом сайте?</h1>
            </Link>
            <p className="app__subtitle">Распределённое web-приложение</p>
          </div>

          {!isLoginPage && (
            <div className="app__header-actions">
              {user ? (
                <>
                  <Link to="/admin" className="auth-button">Админка</Link>
                  <button onClick={logout} className="auth-button">Выйти</button>
                </>
              ) : (
                <Link to="/login" className="auth-button">Войти</Link>
              )}
            </div>
          )}
        </div>
      </header>

      <main className="app__main">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route
            path="/admin"
            element={
              <ProtectedRoute>
                <AdminPage />
              </ProtectedRoute>
            }
          />
        </Routes>
      </main>

      <footer className="app__footer">
        <p>
          GitHub проекта:{' '}
          <a href="https://github.com/Chashniy/docker-nginx-sql" target="_blank" rel="noopener noreferrer">
            github.com/Chashniy/docker-nginx-sql
          </a>
        </p>
        <p>© 2026 Distributed Clock Manager</p>
      </footer>
    </div>
  );
}