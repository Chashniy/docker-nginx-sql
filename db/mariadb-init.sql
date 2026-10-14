-- Пользователи
CREATE TABLE IF NOT EXISTS users (
    id            CHAR(36) PRIMARY KEY,
    username      VARCHAR(64) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role          VARCHAR(16) NOT NULL DEFAULT 'user',
    created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Записи времени (обновлённая)
DROP TABLE IF EXISTS time_records;
CREATE TABLE time_records (
    id          CHAR(36) PRIMARY KEY,
    recorded_at DATETIME NOT NULL,
    created_by  CHAR(36),                    -- FK на users.id, может быть NULL (аноним)
    created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL
);

CREATE INDEX idx_time_records_created_by ON time_records(created_by);