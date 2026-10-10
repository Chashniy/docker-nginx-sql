# Distributed Clock

Учебный проект: распределённая архитектура на Docker с сайтом, который показывает текущее время и дату, и кнопкой сохранения записи в БД.

## 🎯 Что делает

- **Публичная страница** — отображает текущее время и дату, кнопка «Сохранить» пишет запись в БД.
- **API** — принимает запросы от фронтенда, буферизует записи в SQLite и синхронизирует с MariaDB.
- **Админка** *(план)* — таблица записей, редактирование, удаление, авторизация.

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
| `dns-backend` | `dns-backend:dev` | ASP.NET Core API + SQLite outbox |
| `dns-mariadb` | `mariadb:11` | Основное хранилище |

### Роли хранилищ

- **MariaDB** — источник правды. Таблица `time_records`.
- **SQLite** *(план)* — outbox-буфер. Если MariaDB недоступна, записи копятся локально и уходят позже.

## 📁 Структура

```
.
├── apps/
│   ├── backend/          # ASP.NET Core
│   │   ├── Dockerfile
│   │   ├── backend.csproj
│   │   └── Program.cs
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
sudo docker build -t dns-backend:dev .

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

| Метод | Путь | Описание |
|---|---|---|
| `GET` | `/api/health` | Статус backend *(план)* |
| `POST` | `/api/time-records` | Сохранить текущую дату и время *(план)* |
| `GET` | `/api/admin/time-records` | Список записей *(план)* |

## 🗄️ Схема БД

### MariaDB

```sql
CREATE TABLE time_records (
    id CHAR(36) PRIMARY KEY,
    recorded_at DATETIME NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);
```

### SQLite (outbox, план)

```sql
CREATE TABLE outbox (
    id TEXT PRIMARY KEY,
    payload TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'pending',
    attempts INTEGER NOT NULL DEFAULT 0,
    last_error TEXT,
    created_at TEXT NOT NULL,
    next_retry_at TEXT
);
```

## 🎯 Статус

- [x] Инфраструктура: Docker-сеть, 4 контейнера
- [x] Nginx как reverse-proxy
- [x] MariaDB + таблица `time_records`
- [x] Backend: ASP.NET Core (заглушка)
- [x] Frontend: React + TypeScript + Vite (заглушка)
- [ ] Backend: `POST /api/time-records`
- [ ] Backend: SQLite outbox + BackgroundService
- [ ] Frontend: часы, кнопка, индикатор статуса
- [ ] Auth: логин + пароль
- [ ] Админка: CRUD для записей

## 📌 Заметки

- Данные контейнеров лежат в `data/` и **не коммитятся** в git.
- Пароли в командах `docker run` — **учебные**. В продакшене — через `.env` и секреты.
- Nginx использует `resolver 127.0.0.11` для Docker DNS — чтобы не падать, если backend/frontend недоступны.