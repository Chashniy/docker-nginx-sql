import { useEffect, useState } from 'react';

type HealthStatus = 'loading' | 'ok' | 'error';

export default function StatusIndicator() {
  const [status, setStatus] = useState<HealthStatus>('loading');
  const [lastCheck, setLastCheck] = useState<Date | null>(null);

  async function check() {
    try {
      const res = await fetch('/api/health', { credentials: 'include' });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      setStatus('ok');
    } catch {
      setStatus('error');
    } finally {
      setLastCheck(new Date());
    }
  }

  useEffect(() => {
    check();
    const timer = setInterval(check, 10_000);   // каждые 10 секунд
    return () => clearInterval(timer);
  }, []);

  const label =
    status === 'loading' ? 'Проверка...'
    : status === 'ok' ? 'Backend работает'
    : 'Backend недоступен';

  const cls =
    status === 'loading' ? 'status--loading'
    : status === 'ok' ? 'status--ok'
    : 'status--error';

  return (
    <div className={`status ${cls}`}>
      <span className="status__dot" />
      <span className="status__label">{label}</span>
      {lastCheck && (
        <span className="status__time">
          {lastCheck.toLocaleTimeString('ru-RU')}
        </span>
      )}
    </div>
  );
}