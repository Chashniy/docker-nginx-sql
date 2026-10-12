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
5. [Nginx: конфигурация](#nginx-конфигурация)
6. [Nginx: proxy_pass и переменные](#nginx-proxy_pass-и-переменные)
7. [Nginx: POST без тела](#nginx-post-без-тела)
8. [SQLite: readonly database](#sqlite-readonly-database)
9. [SQLite: WAL-режим](#sqlite-wal-режим)
10. [EF Core: Guid vs string](#ef-core-guid-vs-string)
11. [EF Core: snake_case vs PascalCase](#ef-core-snake_case-vs-pascalcase)
12. [ASP.NET: AddHostedService после Build](#aspnet-addhostedservice-после-build)
13. [TypeScript: top-level statements](#typescript-top-level-statements)
14. [TypeScript: опечатки в именах типов](#typescript-опечатки-в-именах-типов)
15. [React: пустые файлы](#react-пустые-файлы)
16. [React: default vs named export](#react-default-vs-named-export)

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

## Сводная таблица

| # | Проблема | Симптом | Решение |
|---|---|---|---|
| 1 | `docker run` без `build` | `pull access denied` | Сначала `docker build` |
| 2 | Bind-mount файла | `not a directory` | Создать файл **до** `docker run` |
| 3 | `.dockerignore` пустой | `tsc: not found` | Заполнить `.dockerignore` |
| 4 | `package-lock.json` glibc | `Exit handler never called` | Удалить lock + `node_modules` |
| 5 | Logging driver `journald` | `does not support reading` | Переключить на `json-file` |
| 6 | `events` не в главном конфиге | `no "events" section` | Полный конфиг в `nginx.conf` |
| 7 | `events` в `conf.d` | `"events" not allowed here` | Фрагмент в `conf.d` |
| 8 | `proxy_pass $var/api/` | `404 от Nginx` | Убрать `/api/` |
| 9 | `POST` без тела | `400 от Nginx` | `-d '{}'` или `fetch` |
| 10 | SQLite root | `readonly database` | `sudo sqlite3` |
| 11 | EF Core `string` vs `Guid` | `InvalidCastException` | Модель `Guid` |
| 12 | `AddHostedService` после `Build` | Сервис не стартует | Перенести **до** `Build()` |
| 13 | Второй `Program.cs` | `CS8802` | Удалить лишний |
| 14 | `Guide` vs `Guid` | `CS0246` | Исправить опечатку |
| 15 | Пустой файл (nano) | Белая страница | Сохранить через `Ctrl+O` |
| 16 | `default` vs `named` export | `doesn't provide default` | `export default` |

---

## Что не вошло в этот файл

- **Продвинутые** темы: CI/CD, Kubernetes, service mesh.
- **Оптимизация**: индексы, кеширование, CDN.
- **Безопасность**: HTTPS, CSRF, rate limiting.

Эти темы — за пределами учебного проекта.

---

## 📖 Полезные ссылки

- [Docker troubleshooting](https://docs.docker.com/config/daemon/)
- [Nginx pitfalls](https://nginx.org/en/docs/)
- [EF Core migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/)
- [SQLite WAL](https://sqlite.org/wal.html)
