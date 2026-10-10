import Clock from './components/Clock';
import SaveButton from './components/SaveButton';
import StatusIndicator from './components/StatusIndicator';
import AuthButton from './components/AuthButton';

export default function App() {
  return (
    <div className="app">
      <header className="app__header">
        <div className="app__header-content">
          <div>
            <h1 className="app__title">Зачем тебе сохранять время на каком-то левом сайте?</h1>
            <p>Распределённое web-приложение</p>
          </div>
          <AuthButton />
        </div>
      </header>

      <main className="app__main">
        <div className="realtime">
          <Clock />
          <SaveButton />
          <StatusIndicator />
        </div>

        <div className="technologies">
          <h2 className="technologies__title">Используемые технологии</h2>
          <ul className="technologies__list">
            <li>Docker + Docker Compose</li>
            <li>Nginx (reverse-proxy)</li>
            <li>MariaDB (основное хранилище)</li>
            <li>SQLite (outbox-буфер)</li>
            <li>ASP.NET Core (backend)</li>
            <li>React + TypeScript (frontend)</li>
            <li>Vite (сборка)</li>
          </ul>
        </div>
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