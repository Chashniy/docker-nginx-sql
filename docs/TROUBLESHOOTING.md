# Troubleshooting: проблемы и решения

Журнал проблем, возникших при разработке Distributed Clock. Каждая запись содержит:
- **Симптом** — что видно.
- **Причина** — почему так.
- **Решение** — что делать.
- **Код** — участок, который вызывал проблему.

---

## Содержание

1. [Docker: образы и контейнеры](#docker-образы-и-контейнеры)
2. [Docker: bind-mount файлов](#docker-bind-mount-файлов)
3. [Docker: сборка frontend](#docker-сборка-frontend)
4. [Docker: logging driver](#docker-logging-driver)
5. [Docker Compose: Access denied](#docker-compose-access-denied)
6. [Docker Compose: изменения не подхватываются](#docker-compose-изменения-не-подхватываются)
7. [Docker Compose: depends_on без healthcheck](#docker-compose-depends_on-без-healthcheck)
8. [Nginx: конфигурация](#nginx-конфигурация)
9. [Nginx: proxy_pass и переменные](#nginx-proxy_pass-и-переменные)
10. [Nginx: POST без тела](#nginx-post-без-тела)
11. [SQLite: readonly database](#sqlite-readonly-database)
12. [SQLite: WAL-режим](#sqlite-wal-режим)
13. [EF Core: Guid vs string](#ef-core-guid-vs-string)
14. [EF Core: snake_case vs PascalCase](#ef-core-snake_case-vs-pascalcase)
15. [ASP.NET: AddHostedService после Build](#aspnet-addhostedservice-после-build)
16. [TypeScript: top-level statements](#typescript-top-level-statements)
17. [TypeScript: опечатки в именах типов](#typescript-опечатки-в-именах-типов)
18. [TypeScript: Property 'role' does not exist](#typescript-property-role-does-not-exist)
19. [React: пустые файлы](#react-пустые-файлы)
20. [React: default vs named export](#react-default-vs-named-export)
21. [Frontend: кнопки накладываются](#frontend-кнопки-накладываются)
22. [Fish: $ в SQL-запросах](#fish--в-sql-запросах)

---

## Docker: образы и контейнеры

### Симптом

```
Unable to find image 'dns-backend:dev' locally
docker: Error response from daemon: pull access denied for dns-backend
```

### Причина

`docker run` **не создаёт** образ. Он **запускает** контейнер из **уже существующего** образа. Если образа **нет** локально — Docker пытается скачать его из **реестра** (Docker Hub). Твоего `dns-backend` там нет — отсюда ошибка.

### Решение

**Сначала** собрать образ:
```bash
sudo docker build -t dns-backend:dev .
```

**Потом** запустить:
```bash
sudo docker run -d --name dns-backend ... dns-backend:dev
```

### Код

`docker run` без предварительного `docker build`.

---

## Docker: bind-mount файлов

### Симптом

```
Error response from daemon: failed to create task for container:
error mounting "...nginx.conf" to rootfs at "/etc/nginx/nginx.conf":
not a directory: Are you trying to mount a directory onto a file?
```

### Причина

Bind-mount **файла** требует, чтобы файл **существовал на хосте ДО** `docker run`. Если файла нет — Docker **создаёт директорию** с таким именем. Приложение внутри контейнера **ожидает файл** — получает директорию — падает.

### Решение

**1.** Создать файл **до** `docker run`:
```bash
nano ~/.localrepo/docker-nginx-sql/config/nginx/nginx.conf
```

**2.** Проверить, что это **файл**, а не директория:
```bash
ls -la ~/.localrepo/docker-nginx-sql/config/nginx/
# Должно быть: -rw-r--r--  nginx.conf
# НЕ:         drwxr-xr-x  nginx.conf
```

**3.** Если уже создалась директория — удалить:
```bash
sudo rm -rf ~/.localrepo/docker-nginx-sql/config/nginx/nginx.conf
```

**4.** Создать заново + `docker run`.

### Код

```bash
sudo docker run -v ~/.localrepo/.../nginx.conf:/etc/nginx/nginx.conf:ro nginx
```

---

## Docker: сборка frontend

### Симптом 1

```
sh: tsc: not found
The command '/bin/sh -c npm run build' returned a non-zero code: 127
```

### Причина

`node_modules` **с хоста** попал в контейнер (из-за пустого `.dockerignore`). В нём — **glibc-бинарники** Rollup (для CachyOS), а контейнер — **Alpine** (musl). `tsc` не находит нужных файлов.

### Симптом 2

```
npm error Exit handler never called!
```

### Причина

Это **баг npm** при установке в Alpine-образе. Может быть из-за несовместимого `package-lock.json` (с хоста) или бага конкретной версии npm.

### Симптом 3

```
Error: Cannot find module @rollup/rollup-linux-x64-musl
```

### Причина

Rollup 4 использует **нативные бинарники**. На CachyOS npm ставит **glibc**-версию (`@rollup/rollup-linux-x64-gnu`), а в Alpine нужна **musl**-версия (`@rollup/rollup-linux-x64-musl`).

### Решение

**1.** Заполнить `.dockerignore`:
```
node_modules
dist
.git
.gitignore
.vscode
.idea
Dockerfile
.dockerignore
npm-debug.log
*.log
.env
.env.*
```

**2.** Удалить `package-lock.json` **с хоста** (он «грязный», с glibc-записями):
```bash
rm ~/.localrepo/.../apps/frontend/package-lock.json
rm -rf ~/.localrepo/.../apps/frontend/node_modules
```

**3.** Использовать **`node:20-alpine`** (не `node:20-slim`, не `node:20`) и **`npm config set fund false`**:
```dockerfile
FROM node:20-alpine AS builder
WORKDIR /app

COPY package*.json ./
RUN npm config set fund false && npm install

COPY . .
RUN npx tsc -b && npx vite build
```

**4.** Пересобрать **с `--network=host`** (на случай проблем с DNS):
```bash
sudo docker build --network=host --no-cache -t dns-frontend:dev .
```

### Код

`Dockerfile`:
```dockerfile
COPY . .
RUN npm run build   # ← tsc не находился из-за node_modules с хоста
```

`.dockerignore` (пустой) — **пропускал** `node_modules` в контекст.

---

## Docker: logging driver

### Симптом

```
Error response from daemon: configured logging driver does not support reading
```

### Причина

В `/etc/docker/daemon.json` или в системе настроен logging driver **`journald`** (или `syslog`). Эти драйверы **не поддерживают** `docker logs`.

### Решение

**1.** Проверить:
```bash
sudo docker info | grep -i "logging driver"
```

**2.** Если `journald` — изменить в `/etc/docker/daemon.json`:
```json
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
```

**3.** Перезапустить Docker:
```bash
sudo systemctl restart docker
```

**Важно:** изменение применяется **только к новым** контейнерам. Старые — придётся **пересоздать**.

### Альтернатива

Читать логи через `journalctl`:
```bash
sudo journalctl CONTAINER_NAME=dns-backend --since "10 minutes ago"
```

---

## Docker Compose: Access denied

### Симптом

```
MySqlConnector.MySqlException (0x80004005): Access denied for user 'clock_user'@'172.19.0.4' (using password: YES)
```

Backend **падает** на `/api/auth/login` (500), но `/api/health` работает (потому что health **не лезет** в MariaDB).

### Причина

Пароли в `.env` **не совпадают** с **реальными** паролями MariaDB.

MariaDB **инициализирует** пользователя **только при первой** инициализации (когда `data/db-data/` **пустая**). Если **меняешь** `.env` **после** — MariaDB **не знает** про **новые** пароли.

**Пример:** ты запускал **раньше** через `docker run` с `MARIADB_PASSWORD=clock_pass`. Потом **перешёл** на `docker compose` с `MARIADB_PASSWORD=clock_pass_change_me`. MariaDB **всё ещё** хочет **`clock_pass`**.

### Решение

**Вариант A (быстро): вернуть старые пароли в `.env`**

```env
MARIADB_ROOT_PASSWORD=root_secret
MARIADB_PASSWORD=clock_pass
MARIADB_CONNECTION=...Password=clock_pass;
```

Затем:
```bash
sudo docker compose down
sudo docker compose up -d
```

**`down` + `up` обязательны!** `restart` **не перечитывает** `.env`.

**Вариант B (правильно): сменить пароли в MariaDB**

```bash
sudo docker exec -it dns-mariadb mariadb -uroot -proot_secret
```
```sql
ALTER USER 'clock_user'@'%' IDENTIFIED BY 'clock_pass_change_me';
ALTER USER 'root'@'localhost' IDENTIFIED BY 'root_secret_change_me';
FLUSH PRIVILEGES;
EXIT;
```

**Если `root_secret` не подходит** — сначала **проверь**, **какой** работает:
```bash
sudo docker exec dns-mariadb mariadb -uroot -proot_secret -e "SELECT 1;"
```

### Код

`Program.cs`:
```csharp
var mariaConnectionString = builder.Configuration.GetConnectionString("MariaDB")
    ?? "Server=dns-mariadb;...Password=clock_pass;";
```

`.env`:
```env
MARIADB_CONNECTION=...Password=clock_pass_change_me;
```

`database` → `Access denied`.

### Правило

`.env` **должен** совпадать с **реальными** паролями MariaDB. Если **меняешь** `.env` для **существующей** БД — **смени** пароль **в БД** (`ALTER USER`) **или** верни **старые** пароли в `.env`.

---

## Docker Compose: изменения не подхватываются

### Симптом

Поменял код **или** `.env`, запустил `docker compose up -d` — **ничего не изменилось**. **Второй** запуск — **заработало**.

### Причина

**Два** разных механизма:

1. **`docker compose up -d`** — **запускает** контейнеры из **уже существующих** образов. **НЕ пересобирает**.
2. **`docker compose restart`** — **перезапускает** контейнер с **теми же** env. **НЕ перечитывает** `.env`.

**Кэш Docker** тоже может помешать: если слой **не изменился**, Docker берёт его **из кэша**.

### Решение

**Изменил код** → пересобрать:
```bash
sudo docker compose up -d --build
```

**Если не помогло** → полный сброс кэша:
```bash
sudo docker compose build --no-cache
sudo docker compose up -d
```

**Изменил `.env`** → **`down` + `up`**:
```bash
sudo docker compose down
sudo docker compose up -d
```

**`restart` не работает** для `.env` — нужен **`down`**.

### Код

```bash
# НЕ работает, если менял .env
sudo docker compose restart backend

# НЕ работает, если менял код
sudo docker compose up -d

# Работает для кода
sudo docker compose up -d --build

# Работает для .env
sudo docker compose down && sudo docker compose up -d
```

### Правило

| Что менял | Команда |
|---|---|
| Код | `docker compose up -d --build` |
| `.env` | `docker compose down && docker compose up -d` |
| Dockerfile | `docker compose build --no-cache && up -d` |
| Ничего | `docker compose restart` |

---

## Docker Compose: depends_on без healthcheck

### Симптом

Backend **падает** при `docker compose up -d`:
```
MySqlException: Unable to connect to any of the specified MySQL hosts.
```

Но **вручную** (`docker compose restart backend`) — **работает**.

### Причина

`depends_on: [mariadb]` **только** задаёт **порядок запуска**. MariaDB **стартует раньше**, но **не готова** принимать соединения — **инициализируется 10–20 секунд**. Backend **лезет** к ней **сразу** — получает **отказ**.

### Решение

Добавить **`healthcheck`** для MariaDB и **`condition: service_healthy`** в `depends_on`:

```yaml
services:
  mariadb:
    image: mariadb:11
    healthcheck:
      test: ["CMD", "healthcheck.sh", "--connect", "--innodb_initialized"]
      interval: 5s
      timeout: 5s
      retries: 10
      start_period: 30s

  backend:
    depends_on:
      mariadb:
        condition: service_healthy   # ← ждём «здоровую» MariaDB
```

Теперь backend **стартует** только когда MariaDB **готова** принимать соединения.

### Код

```yaml
# НЕ работает (просто порядок)
depends_on:
  - mariadb

# Работает (ждём готовности)
depends_on:
  mariadb:
    condition: service_healthy
```

---

## Nginx: конфигурация

### Симптом 1

```
nginx: [emerg] no "events" section in configuration
```

### Причина

Nginx **требует** блоки `events {}` и `http {}` в **главном** конфиге (`/etc/nginx/nginx.conf`). Если туда положить **только** `server {}` — Nginx упадёт.

### Симптом 2

```
nginx: [emerg] "events" directive is not allowed here in /etc/nginx/conf.d/default.conf
```

### Причина

**Обратная** ситуация: `events {}` **нельзя** класть в `conf.d/*.conf`. Только в **главный** `nginx.conf`.

### Решение

**Выбрать одно:**

**Вариант A — полный конфиг в главный файл:**
```nginx
# nginx.conf
events {}

http {
    server {
        listen 80;
        location / { ... }
    }
}
```
```dockerfile
COPY nginx.conf /etc/nginx/nginx.conf
```

**Вариант B — фрагмент в `conf.d`:**
```nginx
# nginx.conf (фрагмент)
server {
    listen 80;
    location / { ... }
}
```
```dockerfile
COPY nginx.conf /etc/nginx/conf.d/default.conf
```

**Не смешивать!** Полный конфиг — **только** в `nginx.conf`, фрагмент — **только** в `conf.d`.

### Код

`Dockerfile` с `COPY nginx.conf /etc/nginx/conf.d/default.conf` + `nginx.conf` **с** `events {}` → ошибка 2.

---

## Nginx: proxy_pass и переменные

### Симптом

```
GET /api/health → 404 Not Found (от Nginx, Content-Length: 0)
```

### Причина

```nginx
location /api/ {
    set $backend "http://dns-backend:8080";
    proxy_pass $backend/api/;   # ← переменная + URI
}
```

Когда `proxy_pass` содержит **переменную**, Nginx **не применяет** нормализацию URI так же, как с **прямым URL**. Результат: запрос уходит на **неправильный** путь (например, `/api/api/health`), backend **не находит** его и возвращает 404.

При этом **`curl http://dns-backend:8080/api/health`** из другого контейнера работает — значит, **backend жив**, проблема **в Nginx**.

### Решение

**Вариант A (проще) — прямой `proxy_pass` без переменной:**
```nginx
location /api/ {
    proxy_pass http://dns-backend:8080;   # ← без /api/ в конце
    proxy_set_header Host $host;
    ...
}
```

Тогда `/api/health` → `http://dns-backend:8080/api/health`. **Правильно.**

**Минус:** Nginx **резолвит** `dns-backend` **при старте**. Если backend **не запущен** — Nginx **упадёт** с `host not found in upstream`.

**Вариант B — переменная + `resolver`:**
```nginx
resolver 127.0.0.11 valid=10s ipv6=off;

location /api/ {
    set $backend "http://dns-backend:8080";
    proxy_pass $backend;   # ← без /api/
    ...
}
```

Nginx резолвит **на каждый запрос** (или раз в 10 сек из кеша). Если backend **лежит** — Nginx вернёт **502**, но **сам не упадёт**.

**Правило:**
- `proxy_pass http://host:port;` — URI **из запроса**.
- `proxy_pass http://host:port/api/;` — URI = `/api/` + **хвост**.
- `proxy_pass $var;` — URI **из запроса**.
- `proxy_pass $var/api/;` — **непредсказуемо**. **Не использовать.**

### Код

```nginx
set $backend "http://dns-backend:8080";
proxy_pass $backend/api/;
```

---

## Nginx: POST без тела

### Симптом

```
POST /api/auth/logout → 400 Bad Request (от Nginx, Content-Length: 0)
```

### Причина

`curl -X POST` **без** `-d` **не отправляет** ни `Content-Length`, ни `Transfer-Encoding`. По HTTP-спецификации это **невалидный** POST-запрос. Nginx **отклоняет** его **до** проксирования на backend.

### Решение

**1.** Для теста — **всегда** отправлять тело:
```bash
curl -i -X POST \
  -H "Content-Type: application/json" \
  -d '{}' \
  http://localhost/api/auth/logout
```

**2.** Для frontend — **не важно**: `fetch` **автоматически** шлёт `Content-Length: 0` для пустого тела.

**3.** Альтернатива — использовать **`DELETE`** для logout:
```csharp
group.MapDelete("/logout", ...);
```
```bash
curl -X DELETE http://localhost/api/auth/logout
```

### Код

```bash
curl -X POST http://localhost/api/auth/logout   # без -d
```

---

## SQLite: readonly database

### Симптом

```
Error in 2nd command line argument: attempt to write a readonly database
```

### Причина

Файл SQLite (`app.db`) **принадлежит `root`** — потому что **backend-контейнер** работает от root и создаёт файлы в bind-mount **от root**. Твой пользователь (`chashniy`) **не может** писать в него **без `sudo`**.

### Решение

**1.** Всегда использовать **`sudo sqlite3`**:
```bash
sudo sqlite3 ~/.localrepo/.../data/backend/app.db "..."
```

**2.** Или изменить владельца (но backend **перезапишет** при следующем запуске):
```bash
sudo chown $USER:$USER ~/.localrepo/.../data/backend/app.db*
```

### Код

```bash
sqlite3 ~/.localrepo/.../data/backend/app.db "DELETE FROM sessions;"
# → readonly
```

---

## SQLite: WAL-режим

### Симптом

Три файла вместо одного:
```
app.db
app.db-shm
app.db-wal
```

### Причина

Это **не проблема** — это **WAL-режим** (`Write-Ahead Logging`). SQLite **пишет** изменения **сначала** в `.wal`, потом **переносит** в `.db`. `.shm` — shared memory для координации.

**Зачем:** параллельное чтение и запись без блокировок.

### Решение

**Ничего не делать** — это **правильно**. WAL включён через:
```sql
PRAGMA journal_mode=WAL;
PRAGMA busy_timeout=5000;
```

### Код

`SqliteContext.Initialize`:
```csharp
pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
```

---

## EF Core: Guid vs string

### Симптом

```
System.InvalidCastException: Unable to cast object of type 'System.Guid' to type 'System.String'.
   at MySqlConnector.Core.Row.GetString(...)
```

### Причина

В БД `id` имеет тип `CHAR(36)` — это **UUID-формат**. MySqlConnector **автоматически** читает его как `Guid` (структуру .NET). А модель C# объявляет `Id` как **`string`**. Отсюда `InvalidCastException`.

**Почему `register` работал, а `login` — нет:**
- `register` использует `AnyAsync` → возвращает **bool**, не читает `Id`.
- `login` использует `FirstOrDefaultAsync` → читает **всю строку**, включая `Id` → падает.

### Решение

Изменить модель `User`:
```csharp
public class User
{
    public Guid Id { get; set; }   // ← было string
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

В `MariaDbContext`:
```csharp
entity.Property(e => e.Id).HasColumnType("char(36)");   // ← явно указать тип
```

В `AuthEndpoints`:
- `Guid.NewGuid()` **без** `.ToString()` при создании.
- `user.Id.ToString()` — **только** там, где нужна строка (в `CreateSession`, в JSON-ответе).

### Код

```csharp
// Модель
public string Id { get; set; } = string.Empty;   // ← ошибка

// Запрос
var user = await db.Users.FirstOrDefaultAsync(...);   // ← падает на Id
```

---

## EF Core: snake_case vs PascalCase

### Симптом

Поля **не мапятся**. EF Core ищет таблицу `Users`, а в БД — `users`. Ищет `Id`, а в БД — `id`.

### Причина

В C# **принят** PascalCase (`Id`, `Username`). В БД — **snake_case** (`id`, `username`). По умолчанию EF Core **использует** имена свойств C#.

### Решение

В `OnModelCreating` **явно** указать маппинг:
```csharp
modelBuilder.Entity<User>(entity =>
{
    entity.ToTable("users");
    entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
    entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(64);
    entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
    entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(16);
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
});
```

Или — глобально через **naming policy** (для продакшена):
```csharp
modelBuilder.UseSnakeCaseNamingConvention();   // ← пакет EFCore.NamingConventions
```

### Код

Модель:
```csharp
public string Username { get; set; }
```

Без `HasColumnName` EF Core сгенерирует `WHERE Username = ...`, а в БД — `username`. Ошибка `Unknown column 'Username'`.

---

## ASP.NET: AddHostedService после Build

### Симптом

`OutboxSyncService` **не запускается**. В логах — **нет** `OutboxSyncService started`.

### Причина

```csharp
var app = builder.Build();                              // ← DI уже собран
builder.Services.AddHostedService<OutboxSyncService>(); // ← слишком поздно
```

`builder.Services` **доступен** только **до** `builder.Build()`. После — сервисы **не добавляются** (или игнорируются).

### Решение

**Перенести** `AddHostedService` **выше** `Build()`:
```csharp
builder.Services.AddHostedService<OutboxSyncService>();

var app = builder.Build();
```

### Код

```csharp
var app = builder.Build();
builder.Services.AddHostedService<OutboxSyncService>();   // ← ошибка
```

**Правило:**
- `builder.Services.AddXxx()` — **до** `Build()`.
- `app.UseXxx()`, `app.MapXxx()` — **после** `Build()`.

---

## TypeScript: top-level statements

### Симптом

```
error CS8802: Only one compilation unit can have top-level statements.
```

### Причина

.NET разрешает top-level statements (`var builder = ...`, без `class`/`Main`) **только в одном файле**. Если компилятор **считает**, что где-то **ещё** есть top-level — ошибка.

**Реальные причины:**
1. Второй `Program.cs` (старый, в корне).
2. Незакрытая `}` в другом файле (компилятор **путается**).
3. Файл без `namespace` — компилятор **считает** его top-level.

### Решение

**1.** Проверить, **один ли** `Program.cs`:
```bash
find ~/.localrepo/.../apps/backend -name "Program.cs"
```

**2.** Проверить **скобки** во всех `.cs` файлах.

**3.** Убедиться, что **у каждого** файла есть `namespace`:
```csharp
namespace Backend.Auth;   // ← обязательно
```

### Код

`AuthEndpoints.cs` без закрывающей `}` класса:
```csharp
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(...)
    {
        ...
    }
// ← нет закрывающей } класса

public record RegisterRequest(...);   // ← компилятор считает top-level
```

---

## TypeScript: опечатки в именах типов

### Симптом

```
error CS0246: The type or namespace name 'Guide' could not be found
```

### Причина

**Опечатка** в имени типа: `Guide` вместо `Guid`. Компилятор **не знает** такого типа.

### Решение

Исправить на `Guid`:
```csharp
public Guid Id { get; set; }   // ← не Guide
```

### Код

```csharp
public Guide Id { get; set; } = string.Empty;   // ← две ошибки: Guide + string.Empty
```

**Правило:** VS Code **подсвечивает** красным несуществующие типы. Смотреть **Problems** (`Ctrl+Shift+M`).

---

## TypeScript: Property 'role' does not exist

### Симптом

```
error TS2339: Property 'role' does not exist on type 'User'.
src/components/ProtectedRoute.tsx(21,28)
```

**Vite dev-server** работает, но **`vite build`** — падает.

### Причина

В **`AuthContext.tsx`** интерфейс `User` **не содержит** поля `role`. Но **`ProtectedRoute.tsx`** **обращается** к `user.role`. TypeScript **не пропускает** такое.

**Vite dev** (`npm run dev`) **игнорирует** ошибки TypeScript — компилирует **на лету** через esbuild. **`vite build`** запускает **`tsc -b`** — **полную** проверку типов.

### Решение

**1.** Обновить **`AuthContext.tsx`**:
```typescript
interface User {
  id: string;
  username: string;
  role: string;   // ← добавили
}
```

**2.** Пересобрать:
```bash
cd apps/frontend
sudo docker build --network=host -t dns-frontend:dev .
```

### Код

`AuthContext.tsx`:
```typescript
interface User {
  id: string;
  username: string;
  // role: string;   ← забыли
}
```

`ProtectedRoute.tsx`:
```tsx
if (requireAdmin && user.role !== 'admin') {   // ← ошибка
```

**Правило:** если **добавляешь** поле в **тип** — **проверь**, что оно **есть** во **всех** файлах, которые **используют** этот тип.

---

## React: пустые файлы

### Симптом

Белая страница. `curl http://localhost` возвращает `Content-Length: 0`.

### Причина

`nano` **не сохраняет** файл, если **не нажать** `Ctrl+O`. Ты **создал** файл (через `touch` или редактор), но **не записал** содержимое. Файл **пустой**.

Vite **собирает** пустой `index.html` → `dist/index.html` пустой → Nginx **раздаёт** пустой ответ.

### Решение

**1.** Проверить **размер** файлов:
```bash
wc -l ~/.localrepo/.../apps/frontend/index.html
wc -l ~/.localrepo/.../apps/frontend/src/main.tsx
wc -l ~/.localrepo/.../apps/frontend/src/App.tsx
```

Если **0** — файл пустой.

**2.** Заполнить через `nano`, **сохранить** (`Ctrl+O` → `Enter` → `Ctrl+X`).

**3.** **Проверить** `wc -l` — не 0.

**4.** Пересобрать frontend:
```bash
sudo docker build --network=host -t dns-frontend:dev .
sudo docker rm -f dns-frontend
sudo docker run -d --name dns-frontend --network dns-net dns-frontend:dev
```

### Код

`nano` без `Ctrl+O`:
```bash
nano file.tsx
# вставил код
# Ctrl+X → "Save modified buffer?" → Y → Enter   ← тоже сохранит
# Или Ctrl+X → "Save modified buffer?" → N       ← НЕ сохранит
```

---

## React: default vs named export

### Симптом

```
The requested module '...AuthButton.tsx' doesn't provide an export named: 'default'
```

### Причина

Импорт **default**, но в файле **нет** `export default`. Например:
```tsx
// App.tsx
import AuthButton from './AuthButton';   // ← default-импорт
```
```tsx
// AuthButton.tsx
export function AuthButton() { ... }   // ← named export, НЕ default
```

### Решение

**Либо** в файле использовать `export default`:
```tsx
// AuthButton.tsx
export default function AuthButton() { ... }
```

**Либо** импортировать **именованно**:
```tsx
// App.tsx
import { AuthButton } from './AuthButton';
```

### Правило

| В файле | Как импортировать |
|---|---|
| `export default function X() {}` | `import X from './file'` |
| `export function X() {}` | `import { X } from './file'` |
| `export const X = () => {}` | `import { X } from './file'` |

### Код

```tsx
// AuthButton.tsx — пустой файл (0 байт)
// App.tsx
import AuthButton from './AuthButton';   // ← default-импорт
```

Пустой файл **не экспортирует** ничего → ошибка.

---

## Frontend: кнопки накладываются

### Симптом

В шапке **две кнопки** («Админка» и «Выйти») **накладываются** друг на друга. Видна **только одна**.

### Причина

Обе кнопки имеют `position: absolute` с **одинаковыми** `top: 1.5rem; right: 1.5rem`. Они **вырываются** из потока и **прилипают** к **одной** точке.

```css
.auth-button {
  position: absolute;   /* ← каждая кнопка absolute */
  top: 1.5rem;
  right: 1.5rem;
}
```

### Решение

**1.** Обернуть кнопки в **контейнер** `.app__header-actions`.

**2.** `position: absolute` — **на контейнер**, **не** на кнопки:
```css
.app__header-actions {
  position: absolute;
  top: 1.5rem;
  right: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  align-items: flex-end;
}

.auth-button {
  /* position: static — убрали absolute */
  padding: 0.5rem 1rem;
  /* ... */
}
```

### Код

```css
/* НЕ работает: обе кнопки absolute, накладываются */
.auth-button {
  position: absolute;
  top: 1.5rem;
  right: 1.5rem;
}
```

---

## Fish: $ в SQL-запросах

### Симптом

```
fish: $. is not a valid variable in fish.
  "SELECT json_extract(payload, '$.created_by') FROM outbox;"
                                              ^
```

### Причина

В **fish** символ `$` — **всегда** начало **переменной**. `$` в **строке** (`'$.created_by'`) **интерпретируется** как **переменная** — отсюда ошибка.

**В bash** — `$` **тоже** переменная, но `'$.created_by'` в **одинарных** кавычках — **литерал**. В **fish** — **нет**, `$` **работает** **внутри** кавычек.

### Решение

**1.** Экранировать `$`:
```bash
sudo sqlite3 ~/.localrepo/.../app.db \
  "SELECT json_extract(payload, '\$.created_by') FROM outbox;"
```

**2.** Или **избегать** `$` в запросе:
```bash
sudo sqlite3 ~/.localrepo/.../app.db "SELECT payload FROM outbox;"
```
`payload` — **весь** JSON. **Видно** `created_by` **глазами**.

**3.** Или использовать **`bash -c`**:
```bash
bash -c 'sudo sqlite3 ~/.localrepo/.../app.db "SELECT json_extract(payload, \"\$.created_by\") FROM outbox;"'
```

### Код

```bash
# В fish — НЕ работает
sudo sqlite3 app.db "SELECT json_extract(payload, '$.created_by') FROM outbox;"

# В fish — работает
sudo sqlite3 app.db "SELECT json_extract(payload, '\$.created_by') FROM outbox;"
```

**Правило:** в **fish** экранируй `$` как `\$`, или **избегай** `$` в SQL. Или используй **bash**.

---

## Сводная таблица

| # | Проблема | Симптом | Решение |
|---|---|---|---|
| 1 | `docker run` без `build` | `pull access denied` | Сначала `docker build` |
| 2 | Bind-mount файла | `not a directory` | Создать файл **до** `docker run` |
| 3 | `.dockerignore` пустой | `tsc: not found` | Заполнить `.dockerignore` |
| 4 | `package-lock.json` glibc | `Exit handler never called` | Удалить lock + `node_modules` |
| 5 | Logging driver `journald` | `does not support reading` | Переключить на `json-file` |
| 6 | **Пароли `.env` ≠ MariaDB** | **`Access denied`** | **Вернуть старые пароли или `ALTER USER`** |
| 7 | **`up` без `--build`** | **Изменения не подхватились** | **`--build` или `--no-cache`** |
| 8 | **`restart` и `.env`** | **Изменения не подхватились** | **`down` + `up`** |
| 9 | **`depends_on` без `condition`** | **Backend падает на старте** | **`condition: service_healthy`** |
| 10 | `events` не в главном конфиге | `no "events" section` | Полный конфиг в `nginx.conf` |
| 11 | `events` в `conf.d` | `"events" not allowed here` | Фрагмент в `conf.d` |
| 12 | `proxy_pass $var/api/` | `404 от Nginx` | Убрать `/api/` |
| 13 | `POST` без тела | `400 от Nginx` | `-d '{}'` или `fetch` |
| 14 | SQLite root | `readonly database` | `sudo sqlite3` |
| 15 | EF Core `string` vs `Guid` | `InvalidCastException` | Модель `Guid` |
| 16 | `AddHostedService` после `Build` | Сервис не стартует | Перенести **до** `Build()` |
| 17 | Второй `Program.cs` | `CS8802` | Удалить лишний |
| 18 | `Guide` vs `Guid` | `CS0246` | Исправить опечатку |
| 19 | **`role` не в типе `User`** | **`TS2339`** | **Добавить `role` в `AuthContext`** |
| 20 | Пустой файл (nano) | Белая страница | Сохранить через `Ctrl+O` |
| 21 | `default` vs `named` export | `doesn't provide default` | `export default` |
| 22 | **Кнопки absolute** | **Накладываются** | **`absolute` на контейнер, не на кнопки** |
| 23 | **`$` в fish** | **`not a valid variable`** | **Экранировать `\$` или bash** |

---

## Что не вошло в этот файл

- **Продвинутые** темы: CI/CD, Kubernetes, service mesh.
- **Оптимизация**: индексы, кеширование, CDN.
- **Безопасность**: HTTPS, CSRF, rate limiting.

Эти темы — за пределами учебного проекта.

---

## 📖 Полезные ссылки

- [Docker troubleshooting](https://docs.docker.com/config/daemon/)
- [Docker Compose troubleshooting](https://docs.docker.com/compose/)
- [Nginx pitfalls](https://nginx.org/en/docs/)
- [EF Core migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/)
- [SQLite WAL](https://sqlite.org/wal.html)
- [React Router](https://reactrouter.com)