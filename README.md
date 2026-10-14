# Distributed Clock

Учебный проект: распределённая веб-архитектура на Docker. Сайт показывает текущее время и дату, кнопка сохранения пишет запись в БД. Реализована авторизация (логин + пароль, без email) и планируется админка для управления записями.

## 🎯 Что делает

- **Публичная страница** (`/`) — отображает текущее время и дату, кнопка «Сохранить» пишет запись в БД.
- **Авторизация** (`/login`) — регистрация и вход по логину/паролю.
- **Админка** (`/admin`) — таблица записей, редактирование, удаление *(в разработке)*.
- **API** — принимает запросы от фронтенда, буферизует записи в SQLite и синхронизирует с MariaDB.

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
| `dns-nginx` | `nginx:1.27-alpine` | Reverse-proxy, торчит на порту 80 |
| `dns-frontend` | `dns-frontend:dev` | React-статика, раздаётся через Nginx |
| `dns-backend` | `dns-backend:dev` | ASP.NET Core API + SQLite |
| `dns-mariadb` | `mariadb:11` | Основное хранилище |

### Роли хранилищ

- **MariaDB** — источник правды. Таблицы `users`, `time_records`.
- **SQLite** — две роли:
  - `sessions` — сессии авторизации (cookie-based auth).
  - `outbox` — буфер для записей времени перед синхронизацией с MariaDB.

### Поток данных

```
Frontend → POST /api/time-records
              ↓
        Backend принимает
              ↓
        Пишет в SQLite outbox (status='pending')
              ↓
        Отвечает 202 Accepted
              ↓
        BackgroundService (раз в 5 сек)
              ↓
        Идемпотентно вставляет в MariaDB
              ↓
        Помечает status='synced'
```

Это паттерн **Transactional Outbox** — устойчивость к падению MariaDB.

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
├── config/
│   └── nginx/            # nginx.conf внешнего балансировщика
├── data/                 # Персистентные данные (не в git)
│   ├── db-data/          # MariaDB
│   └── backend/          # SQLite
├── db/
│   └── mariadb-init.sql  # Схема БД при первой инициализации
└── README.md
```

## 🚀 Запуск

### Предварительно

- Docker и Docker Compose
- Сеть `dns-net`: `sudo docker network create dns-net`

### Сборка образов

```bash
# Backend
cd apps/backend
sudo docker build --network=host -t dns-backend:dev .

# Frontend
cd apps/frontend
sudo docker build --network=host -t dns-frontend:dev .
```

### Запуск контейнеров

```bash
# 1. MariaDB
sudo docker run -d \
  --name dns-mariadb \
  --network dns-net \
  -e MARIADB_ROOT_PASSWORD=root_secret \
  -e MARIADB_DATABASE=clock_db \
  -e MARIADB_USER=clock_user \
  -e MARIADB_PASSWORD=clock_pass \
  -v $PWD/data/db-data:/var/lib/mysql \
  -v $PWD/db/mariadb-init.sql:/docker-entrypoint-initdb.d/init.sql:ro \
  mariadb:11

# 2. Backend
sudo docker run -d \
  --name dns-backend \
  --network dns-net \
  -v $PWD/data/backend:/app/data \
  dns-backend:dev

# 3. Frontend
sudo docker run -d \
  --name dns-frontend \
  --network dns-net \
  dns-frontend:dev

# 4. Внешний Nginx
sudo docker run -d \
  --name dns-nginx \
  --network dns-net \
  -p 80:80 \
  -v $PWD/config/nginx/nginx.conf:/etc/nginx/nginx.conf:ro \
  nginx:1.27-alpine
```

### Проверка

```bash
sudo docker ps
curl http://localhost/api/health
curl http://localhost
```

## 🛠️ Остановка и запуск

```bash
# Остановить все контейнеры
sudo docker stop dns-nginx dns-frontend dns-backend dns-mariadb

# Запустить обратно
sudo docker start dns-mariadb dns-backend dns-frontend dns-nginx
```

## 📊 API

### Публичные

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/health` | Статус backend |
| `POST` | `/api/time-records` | Сохранить текущую дату и время (202 Accepted) |

### Auth

| Метод | Путь | Описание |
|---|---|---|
| `POST` | `/api/auth/register` | Регистрация (создаёт пользователя + сессию) |
| `POST` | `/api/auth/login` | Вход (создаёт сессию) |
| `POST` | `/api/auth/logout` | Выход (удаляет сессию) |
| `GET` | `/api/auth/me` | Текущий пользователь (или 401) |

### Admin *(в разработке)*

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
-- Сессии авторизации
CREATE TABLE sessions (
    id         TEXT PRIMARY KEY,
    user_id    TEXT NOT NULL,
    username   TEXT NOT NULL,
    created_at TEXT NOT NULL,
    expires_at TEXT NOT NULL
);

-- Outbox для синхронизации с MariaDB
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
- [x] MariaDB + таблицы `users`, `time_records`
- [x] Backend: ASP.NET Core
- [x] SQLite: сессии + outbox
- [x] Auth: регистрация, логин, логаут, `/me`
- [x] `POST /api/time-records` (аноним + авторизован)
- [x] OutboxSyncService: синхронизация SQLite → MariaDB
- [ ] Admin API: `GET/PUT/DELETE /api/admin/time-records`
- [ ] Frontend: роутинг (`/`, `/login`, `/admin`)
- [ ] Frontend: форма логина/регистрации
- [ ] Frontend: админка с таблицей
- [ ] Frontend: живые компоненты (часы, кнопка, статус)
- [ ] `docker-compose.yml`
- [ ] `.env` для секретов

## 📌 Заметки

- Данные контейнеров лежат в `data/` и **не коммитятся** в git.
- Пароли в командах `docker run` — **учебные**. В продакшене — через `.env` и секреты.
- Nginx использует `resolver 127.0.0.11` для Docker DNS — чтобы не падать, если backend/frontend недоступны.
- SQLite работает в WAL-режиме (`journal_mode=WAL`) для параллельного чтения/записи.
- Пароли хешируются через BCrypt (work factor 11).
- Сессии хранятся в SQLite, cookie — HttpOnly + SameSite=Lax.