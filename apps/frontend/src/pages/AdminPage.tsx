import { useEffect, useState } from 'react';
import { useAuth } from '../AuthContext';

interface TimeRecord {
  id: string;
  recordedAt: string;
  createdBy: string | null;
  username: string | null;
  createdAt: string;
}

export default function AdminPage() {
  const { user } = useAuth();
  const [records, setRecords] = useState<TimeRecord[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');

  async function loadRecords() {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch('/api/admin/time-records', { credentials: 'include' });
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }
      const data = await res.json();
      setRecords(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Ошибка загрузки');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadRecords();
  }, []);

  async function handleDelete(id: string) {
    if (!confirm('Удалить запись?')) return;

    try {
      const res = await fetch(`/api/admin/time-records/${id}`, {
        method: 'DELETE',
        credentials: 'include',
      });
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }
      setRecords(records.filter(r => r.id !== id));
    } catch (err) {
      alert('Ошибка удаления: ' + (err instanceof Error ? err.message : ''));
    }
  }

  function startEdit(record: TimeRecord) {
    setEditingId(record.id);
    // datetime-local требует формат YYYY-MM-DDTHH:MM
    setEditValue(record.recordedAt.slice(0, 16));
  }

  async function saveEdit(id: string) {
    try {
      const res = await fetch(`/api/admin/time-records/${id}`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ recordedAt: editValue + ':00Z' }),
      });
      if (!res.ok) {
        throw new Error(`HTTP ${res.status}`);
      }
      setEditingId(null);
      await loadRecords();
    } catch (err) {
      alert('Ошибка сохранения: ' + (err instanceof Error ? err.message : ''));
    }
  }

  function formatDate(iso: string): string {
    try {
      return new Date(iso).toLocaleString('ru-RU');
    } catch {
      return iso;
    }
  }

  if (loading) {
    return <div className="admin">Загрузка...</div>;
  }

  if (error) {
    return <div className="admin">Ошибка: {error}</div>;
  }

  return (
    <div className="admin">
      <div className="admin__header">
        <h2 className="admin__title">
          Записи времени ({records.length})
        </h2>
        <button onClick={loadRecords} className="btn">
          Обновить
        </button>
      </div>

      <p className="admin__user">
        Вы вошли как: <strong>{user?.username}</strong>
      </p>

      {records.length === 0 ? (
        <p className="admin__empty">Записей пока нет.</p>
      ) : (
        <div className="admin__table-wrapper">
          <table className="admin__table">
            <thead>
              <tr>
                <th>ID</th>
                <th>Время записи</th>
                <th>Пользователь</th>
                <th>Создано</th>
                <th>Действия</th>
              </tr>
            </thead>
            <tbody>
              {records.map(record => (
                <tr key={record.id}>
                  <td className="admin__cell-id">{record.id.slice(0, 8)}...</td>
                  <td>
                    {editingId === record.id ? (
                      <input
                        type="datetime-local"
                        value={editValue}
                        onChange={(e) => setEditValue(e.target.value)}
                        className="login-form__input"
                      />
                    ) : (
                      formatDate(record.recordedAt)
                    )}
                  </td>
                  <td>{record.username ?? <span className="admin__anon">аноним</span>}</td>
                  <td>{formatDate(record.createdAt)}</td>
                  <td className="admin__actions">
                    {editingId === record.id ? (
                      <>
                        <button onClick={() => saveEdit(record.id)} className="btn btn--small">
                          Сохранить
                        </button>
                        <button onClick={() => setEditingId(null)} className="btn btn--small">
                          Отмена
                        </button>
                      </>
                    ) : (
                      <>
                        <button onClick={() => startEdit(record)} className="btn btn--small">
                          Изменить
                        </button>
                        <button onClick={() => handleDelete(record.id)} className="btn btn--small btn--danger">
                          Удалить
                        </button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}