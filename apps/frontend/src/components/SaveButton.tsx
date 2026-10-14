import { useState } from 'react';

export default function SaveButton() {
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [message, setMessage] = useState('');

  async function handleClick() {
    setSaving(true);
    setStatus('idle');
    setMessage('');

    try {
      const res = await fetch('/api/time-records', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: '{}',
      });

      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }

      const data = await res.json();
      setStatus('success');
      setMessage(`Сохранено: ${new Date(data.recordedAt).toLocaleString('ru-RU')}`);

      // Сброс через 3 секунды
      setTimeout(() => {
        setStatus('idle');
        setMessage('');
      }, 3000);
    } catch (err) {
      setStatus('error');
      setMessage('Ошибка: ' + (err instanceof Error ? err.message : 'неизвестно'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="save-block">
      <button
        onClick={handleClick}
        className="save-button"
        disabled={saving}
      >
        {saving ? 'Сохранение...' : 'Сохранить'}
      </button>

      {message && (
        <div className={`save-message save-message--${status}`}>
          {message}
        </div>
      )}
    </div>
  );
}