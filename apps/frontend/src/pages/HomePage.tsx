import Clock from '../components/Clock';
import SaveButton from '../components/SaveButton';
import StatusIndicator from '../components/StatusIndicator';

export default function HomePage() {
  return (
    <>
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
    </>
  );
}