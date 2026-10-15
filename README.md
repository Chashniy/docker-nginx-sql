# Distributed Clock

Учебный проект: распределённое веб-приложение на Docker. Сайт показывает текущее время и дату, кнопка «Сохранить» пишет запись в БД. Реализованы авторизация (логин + пароль), роли (`admin` / `user`) и админка.

## 🎯 Что делает

- **Публичная страница** (`/`) — текущее время, дата, кнопка «Сохранить».
- **Авторизация** (`/login`) — регистрация и вход по логину/паролю.
- **Админка** (`/admin`) — таблица записей, редактирование, удаление (только для `admin`).
- **API** — принимает запросы, буферизует в SQLite outbox, синхронизирует с MariaDB.

## 🏗️ Архитектура

```
Браузер → dns-nginx (порт 80) → dns-frontend  (React + TypeScript)
                              → dns-backend   (ASP.NET Core)
                              → dns-mariadb   (MariaDB)
```

Всё работает в Docker-сети `dns-net`. Контейнеры видят друг друга по именам.

### Контейнеры

| Контейнер | Образ | Роль |
|---|---|---|
| `dns-nginx` | `nginx:1.27-alpine` | Reverse-proxy, порт 80 |
| `dns-frontend` | `dns-frontend:dev` | React-статика |
| `dns-backend` | `dns-backend:dev` | ASP.NET Core API + SQLite |
| `dns-mariadb` | `mariadb:11` | Основное хранилище |

### Хранилища

- **MariaDB** — источник правды. Таблицы `users`, `time_records`.
- **SQLite** — две роли:
  - `sessions` — сессии авторизации (cookie-based auth).
  - `outbox` — буфер для записей перед синхронизацией с MariaDB (паттерн **Transactional Outbox**).

### Поток данных

```
Frontend → POST /api/time-records
              ↓
        Backend принимает (берёт user_id из сессии)
              ↓
        Пишет в SQLite outbox (status='pending')
              ↓
        Отвечает 202 Accepted
              ↓
        OutboxSyncService (раз в 5 сек)
              ↓
        Идемпотентно вставляет в MariaDB
              ↓
        Помечает status='synced'
```

## 📁 Структура

```
.
├── apps/
│   ├── backend/          # ASP.NET Core
│   │   ├── Dockerfile
│   │   ├── backend.csproj
│   │   └── src/
│   │       ├── Program.cs
│   │       ├── Auth/             # PasswordHasher, SessionMiddleware, AuthEndpoints
│   │       ├── Data/             # MariaDbContext, SqliteContext
│   │       ├── Models/           # User, TimeRecord
│   │       ├── Outbox/           # OutboxRepository, OutboxSyncService
│   │       └── TimeRecords/      # TimeRecordsEndpoints, AdminEndpoints
│   └── frontend/         # React + TypeScript + Vite
│       ├── Dockerfile
│       ├── package.json
│       ├── nginx.conf
│       └── src/
│           ├── AuthContext.tsx
│           ├── App.tsx
│           ├── pages/            # HomePage, LoginPage, AdminPage
│           └── components/       # Clock, SaveButton, StatusIndicator, ProtectedRoute
├── config/
│   └── nginx/            # nginx.conf внешнего балансировщика
├── data/                 # Персистентные данные (не в git)
│   ├── db-data/          # MariaDB
│   └── backend/          # SQLite
├── db/
│   └── mariadb-init.sql  # Схема БД
├── docs/
│   ├── ARCHITECTURE.md   # Подробное устройство, все термины
│   └── TROUBLESHOOTING.md # Проблемы и решения
├── .env.example
├── .gitignore
├── docker-compose.yml
└── README.md
```

## 🚀 Запуск

### Предварительно

- Docker + Docker Compose
- Собраны образы:
  ```bash
  cd apps/backend && sudo docker build --network=host -t dns-backend:dev .
  cd apps/frontend && sudo docker build --network=host -t dns-frontend:dev .
  ```

### Настройка

Скопируй `.env.example` в `.env` и **поменяй пароли**:
```bash
cp .env.example .env
nano .env
```

### Запуск

```bash
sudo docker compose up -d
```

### Проверка

```bash
sudo docker compose ps
curl http://localhost/api/health
```

Открой `http://localhost` — увидишь сайт.

### Остановка

```bash
sudo docker compose down       # остановить (данные сохранятся)
sudo docker compose down -v    # остановить + удалить volumes
```

## 🔐 Первый пользователь

**Первый** зарегистрированный пользователь автоматически становится **`admin`**. Остальные — **`user`**.

**Регистрация:** `http://localhost/login` → «Нет аккаунта? Зарегистрироваться».

**Админка** доступна только `admin` — ссылка «Перейти в админку →» появится на главной.

## 📊 API

### Публичные

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/health` | Статус backend |
| `POST` | `/api/time-records` | Сохранить время (202 Accepted) |

### Auth

| Метод | Путь | Описание |
|---|---|---|
| `POST` | `/api/auth/register` | Регистрация |
| `POST` | `/api/auth/login` | Вход |
| `POST` | `/api/auth/logout` | Выход |
| `GET` | `/api/auth/me` | Текущий пользователь |

### Admin (только для `admin`)

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/admin/time-records` | Список записей |
| `PUT` | `/api/admin/time-records/{id}` | Редактирование |
| `DELETE` | `/api/admin/time-records/{id}` | Удаление |

## 🗄️ Схема БД

### MariaDB

```sql
CREATE TABLE users (
    id            CHAR(36) PRIMARY KEY,
    username      VARCHAR(64) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    role          VARCHAR(16) NOT NULL DEFAULT 'user',
    created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE time_records (
    id          CHAR(36) PRIMARY KEY,
    recorded_at DATETIME NOT NULL,
    created_by  CHAR(36),
    created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (created_by) REFERENCES users(id) ON DELETE SET NULL
);
```

### SQLite

```sql
CREATE TABLE sessions (
    id         TEXT PRIMARY KEY,
    user_id    TEXT NOT NULL,
    username   TEXT NOT NULL,
    created_at TEXT NOT NULL,
    expires_at TEXT NOT NULL
);

CREATE TABLE outbox (
    id            TEXT PRIMARY KEY,
    payload       TEXT NOT NULL,
    status        TEXT NOT NULL DEFAULT 'pending',
    attempts      INTEGER NOT NULL DEFAULT 0,
    last_error    TEXT,
    created_at    TEXT NOT NULL,
    next_retry_at TEXT
);
```

## 🎯 Статус

- [x] Инфраструктура: Docker-сеть, 4 контейнера
- [x] Nginx как reverse-proxy
- [x] MariaDB: `users`, `time_records` + роли
- [x] Backend: ASP.NET Core
- [x] SQLite: сессии + outbox
- [x] Auth: регистрация, логин, логаут, `/me`
- [x] Роли: первый пользователь = `admin`
- [x] `POST /api/time-records` (аноним + авторизован)
- [x] OutboxSyncService: синхронизация SQLite → MariaDB
- [x] Admin API: `GET/PUT/DELETE /api/admin/time-records`
- [x] Frontend: роутинг, формы, админка, живые компоненты
- [x] `docker-compose.yml` + `.env`

## 📌 Заметки

- Данные контейнеров — в `data/`, **не коммитятся**.
- Пароли в `.env` — **не коммитятся**.
- SQLite в **WAL-режиме** (`journal_mode=WAL`) для параллельного чтения/записи.
- Пароли хешируются **BCrypt** (work factor 11).
- Сессии — в SQLite, cookie — `HttpOnly` + `SameSite=Lax`.
- Nginx использует `resolver 127.0.0.11` для Docker DNS.
- `restart: unless-stopped` — автозапуск после ребута.

## 📖 Документация

- **[ARCHITECTURE.md](docs/ARCHITECTURE.md)** — подробное устройство, все термины.
- **[TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)** — проблемы и решения.
