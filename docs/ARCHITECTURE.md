# Архитектура Distributed Clock

Учебный документ. Объясняет **все** термины и механизмы, которые используются в проекте. Написан для тех, кто впервые пишет веб-приложение.

## 📚 Содержание

1. [Общая картина](#общая-картина)
2. [Слои приложения](#слои-приложения)
3. [Термины](#термины)
4. [Как проходит запрос](#как-проходит-запрос)
5. [Как работает авторизация](#как-работает-авторизация)
6. [Как работает outbox](#как-работает-outbox)
7. [Docker: контейнеры и сети](#docker-контейнеры-и-сети)
8. [Frontend: React + Vite](#frontend-react--vite)
9. [Backend: ASP.NET Core](#backend-aspnet-core)

---

## Общая картина

Сайт — это **распределённое** приложение. Это значит, что **разные части** работают в **разных процессах** (контейнерах) и общаются **по сети**.

```
┌─────────────┐
│   Браузер   │  ← пользователь открывает http://localhost
└──────┬──────┘
       │ HTTP
       ▼
┌─────────────┐
│  dns-nginx  │  ← reverse-proxy: решает, куда направить запрос
└──────┬──────┘
       │
   ┌───┴────┐
   │        │
   ▼        ▼
┌──────┐  ┌──────────┐
│front │  │ backend  │
│ end  │  │ ASP.NET  │
└──────┘  └────┬─────┘
               │
          ┌────┴────┐
          ▼         ▼
      ┌───────┐ ┌──────┐
      │MariaDB│ │SQLite│
      └───────┘ └──────┘
```

**Ключевая идея:** каждый компонент **отвечает за своё**. Frontend — рисует. Backend — обрабатывает логику. MariaDB — хранит данные. SQLite — быстрый локальный буфер.

---

## Слои приложения

### 1. Frontend (клиент)

**Что это:** код, который **выполняется в браузере** пользователя.

**Технологии:**
- **React** — библиотека для построения UI из **компонентов**.
- **TypeScript** — JavaScript с **типами**.
- **Vite** — сборщик (превращает `.tsx` в `.js`, `.css`).

**Что делает:**
- Показывает страницу.
- Реагирует на клики.
- Отправляет запросы к backend через **HTTP**.

**Где живёт:** в контейнере `dns-frontend`, раздаётся через Nginx.

### 2. Backend (сервер)

**Что это:** код, который **выполняется на сервере**. Пользователь его **не видит**.

**Технологии:**
- **ASP.NET Core** — фреймворк для веб-API на C#.

**Что делает:**
- Принимает HTTP-запросы.
- Проверяет авторизацию.
- Пишет/читает данные из БД.
- Отвечает JSON'ом.

**Где живёт:** контейнер `dns-backend`, слушает порт 8080 (внутри сети).

### 3. Базы данных

**MariaDB** — основное хранилище. Здесь **постоянно** лежат:
- пользователи (`users`),
- записи времени (`time_records`).

**SQLite** — **локальный файл** внутри backend-контейнера. Здесь:
- сессии авторизации (`sessions`),
- буфер для синхронизации (`outbox`).

**Почему две БД?**
- **MariaDB** — сетевая, многопользовательская, для **важных** данных.
- **SQLite** — файловая, быстрая, для **временных** и **промежуточных** данных.

### 4. Nginx (reverse-proxy)

**Что это:** сервер, который **стоит перед** остальными и **распределяет** запросы.

**Что делает:**
- Слушает порт 80.
- Если запрос `/api/*` — отправляет на backend.
- Если `/` — отправляет на frontend.

**Зачем:** один **вход** для всех. Браузер **не знает**, что за Nginx'ом **четыре** контейнера. Он видит **один** сайт на `http://localhost`.

---

## Термины

### HTTP

**Протокол** общения между клиентом и сервером. Запросы бывают:
- **GET** — получить данные.
- **POST** — создать/отправить.
- **PUT** — обновить.
- **DELETE** — удалить.

**Ответ** содержит:
- **Статус-код** (`200 OK`, `404 Not Found`, `401 Unauthorized`, `500 Internal Server Error`).
- **Заголовки** (метаданные: тип контента, cookies).
- **Тело** (данные: HTML, JSON).

### JSON

**Формат** передачи данных. Выглядит как JavaScript-объект:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "admin",
  "createdAt": "2026-10-10T17:00:00Z"
}
```

Используется **везде**: backend отдаёт JSON, frontend его парсит.

### REST API

**Стиль** построения API. Каждый **ресурс** имеет **URL**:
- `/api/time-records` — коллекция записей.
- `/api/time-records/{id}` — одна запись.
- `/api/auth/login` — действие (не совсем «ресурс», но допустимо).

**Метод** + **URL** = **действие**:
- `GET /api/time-records` — получить все.
- `POST /api/time-records` — создать.
- `PUT /api/time-records/{id}` — обновить.
- `DELETE /api/time-records/{id}` — удалить.

### Endpoint (эндпоинт)

**Точка входа** в API. Конкретный **URL + метод**. Например:
- `POST /api/auth/register` — эндпоинт регистрации.
- `GET /api/auth/me` — эндпоинт «кто я».

### Middleware (мидлварь)

**Прослойка** между запросом и эндпоинтом. В ASP.NET Core — **цепочка** middleware. Каждый **может**:
- Обработать запрос.
- Передать **дальше** (`await _next(context)`).
- Прервать цепочку.

**В нашем проекте:** `SessionMiddleware` — читает cookie, ищет сессию, кладёт `user_id` в `HttpContext.Items`. Если сессии нет — просто **пропускает** дальше (запрос идёт как «анонимный»).

### Cookie

**Маленький кусочек данных**, который **сервер** отправляет **браузеру**. Браузер **сам** прикладывает его к **каждому** следующему запросу на **тот же домен**.

**Пример:**
```
Set-Cookie: session_id=abc123; HttpOnly; SameSite=Lax; expires=...
```

- **`session_id=abc123`** — имя и значение.
- **`HttpOnly`** — JavaScript **не может** прочитать (защита от XSS).
- **`SameSite=Lax`** — не отправлять на сторонние сайты (защита от CSRF).
- **`expires`** — когда cookie **удалится**.

### Session (сессия)

**Способ** запомнить, что пользователь **залогинен**. Сервер создаёт **уникальный ID**, сохраняет его в БД, отдаёт клиенту через **cookie**.

**Клиент** присылает cookie → сервер ищет сессию по ID → находит `user_id` → «пользователь залогинен».

### Хеш пароля

**Нельзя** хранить пароли **в открытом виде**. Если БД утечёт — все пароли **украдут**.

**Решение:** хранить **хеш** — необратимое преобразование. Проверить пароль можно, но **восстановить** — нет.

**BCrypt** — специальный алгоритм, **медленный** (защита от перебора) и **с солью** (одинаковые пароли → разные хеши).

```
password123 → $2a$11$F8yNj0DqYjC6F8yNj0DqYjC6...
```

**В БД** хранится **только** хеш. При логине:
1. Пользователь вводит пароль.
2. Backend **хеширует** его тем же алгоритмом.
3. Сравнивает **хеши**.

### GUID / UUID

**Уникальный идентификатор**. 128-битное число, обычно в формате:
```
550e8400-e29b-41d4-a716-446655440000
```

**Зачем:** вместо автоинкрементных `1, 2, 3` — **глобально уникальные** ID. Можно генерировать **на клиенте** и **не бояться** конфликтов.

**В C#** — тип `Guid`. **В БД** — `CHAR(36)`.

### Idempotency (идемпотентность)

**Свойство** операции: **повторное** выполнение **не меняет** результат.

**Пример:** `INSERT` с **фиксированным** `id` — идемпотентен. Первый раз вставит, второй — **не вставит** (потому что `id` уже есть). Результат — **тот же**.

**Зачем:** если запрос **повторится** (сеть подвисла, retry) — данные **не задублируются**.

---

## Как проходит запрос

### Пример: пользователь нажал «Сохранить»

```
1. React: onClick → fetch('POST /api/time-records', { credentials: 'include' })
   ↓
2. Браузер: отправляет HTTP-запрос на http://localhost/api/time-records
   ↓
3. Nginx: видит /api/ → проксирует на http://dns-backend:8080/api/time-records
   ↓
4. ASP.NET: SessionMiddleware читает cookie, находит user_id, кладёт в Items
   ↓
5. ASP.NET: TimeRecordsEndpoints.MapPost → обработчик
   ↓
6. Обработчик: берёт user_id из Items, вызывает OutboxRepository.AddTimeRecordAsync
   ↓
7. OutboxRepository: пишет запись в SQLite (status='pending'), возвращает id
   ↓
8. Обработчик: возвращает 202 Accepted + JSON
   ↓
9. Nginx: передаёт ответ обратно
   ↓
10. Браузер: fetch получает JSON, React обновляет UI
```

**Через 5 секунд:**
```
11. OutboxSyncService: читает pending из SQLite
12. Вставляет в MariaDB (если id ещё нет)
13. Помечает status='synced'
```

### Почему 202, а не 200?

- **200 OK** — «всё готово».
- **202 Accepted** — «принято, но ещё **не обработано**».

Запись **ещё не в MariaDB**. Она в SQLite. Реальная запись произойдёт **позже**. Поэтому 202.

---

## Как работает авторизация

### Регистрация

```
POST /api/auth/register { "username": "admin", "password": "admin123" }
   ↓
1. Валидация: username >= 3, password >= 6
2. Проверка: username не занят? (SELECT FROM users WHERE username = ...)
3. Создание: INSERT INTO users (id, username, password_hash, created_at)
   ↓ password_hash = BCrypt.Hash("admin123")
4. Создание сессии: INSERT INTO sessions (id, user_id, username, expires_at)
5. Set-Cookie: session_id=<session-id>; HttpOnly; SameSite=Lax
6. Ответ: 200 OK + { id, username }
```

### Логин

```
POST /api/auth/login { "username": "admin", "password": "admin123" }
   ↓
1. SELECT FROM users WHERE username = 'admin'
2. BCrypt.Verify("admin123", user.password_hash)
3. Если ок → создаём сессию (как в регистрации)
4. Set-Cookie + 200 OK
```

### Защищённый запрос

```
GET /api/auth/me (с cookie session_id=abc123)
   ↓
1. SessionMiddleware: читает cookie
2. SELECT FROM sessions WHERE id = 'abc123'
3. Проверка expires_at > now
4. context.Items["user_id"] = session.user_id
5. Endpoint /me: берёт user_id из Items, возвращает 200 OK
```

### Выход

```
POST /api/auth/logout
   ↓
1. Читаем cookie
2. DELETE FROM sessions WHERE id = '<session-id>'
3. Set-Cookie: session_id=; expires=Thu, 01 Jan 1970 ... (удаление)
4. 200 OK
```

---

## Как работает outbox

### Зачем

**Проблема:** если backend **напрямую** пишет в MariaDB, а MariaDB **лежит** — запрос **падает**. Данные **теряются**.

**Решение:** backend пишет **сначала в SQLite** (локально, быстро). Потом **фоновая задача** перекладывает в MariaDB.

**Плюсы:**
- **Быстрый** ответ клиенту (SQLite локально).
- **Устойчивость** к падению MariaDB (данные **в SQLite**, не потеряются).
- **Идемпотентность** (запись в MariaDB по `id` — не задублируется).

### Схема

```sql
CREATE TABLE outbox (
    id            TEXT PRIMARY KEY,   -- GUID
    payload       TEXT NOT NULL,      -- JSON со всеми данными
    status        TEXT NOT NULL,      -- pending | synced | failed
    attempts      INTEGER NOT NULL,   -- сколько раз пытались
    last_error    TEXT,               -- последняя ошибка
    created_at    TEXT NOT NULL,
    next_retry_at TEXT                -- когда повторить
);
```

### Жизненный цикл записи

```
1. INSERT (id, payload, status='pending', attempts=0)
   ↓
2. OutboxSyncService читает pending
   ↓
3. Парсит payload (JSON → объект)
   ↓
4. Проверяет, есть ли запись с таким id в MariaDB
   ↓
5. Если нет — INSERT
   ↓
6. UPDATE status='synced'
```

### При ошибке

```
1. UPDATE attempts = attempts + 1
2. UPDATE last_error = '<сообщение>'
3. UPDATE next_retry_at = now + 2^attempts секунд (экспоненциальная задержка)
4. Если attempts >= 10 → status='failed' (больше не пытаться)
5. Иначе → status='pending' (попробуют позже)
```

**Экспоненциальная задержка:**
- 1-я ошибка → 2 сек.
- 2-я → 4 сек.
- 3-я → 8 сек.
- ...
- 10-я → ~1 час (ограничено).

**Зачем:** если MariaDB **лежит**, не надо **долбить** её каждую секунду. Retry **реже** — но **продолжает** пытаться.

---

## Docker: контейнеры и сети

### Контейнер

**Изолированный** процесс. Внутри — **своя** файловая система, **свои** порты, **своя** сеть.

**Образ (image)** — «шаблон». **Контейнер** — **запущенный** экземпляр образа.

**Dockerfile** — инструкция, как **собрать** образ.

### Multi-stage build

**Проблема:** для **сборки** нужен один инструмент (компилятор), для **запуска** — другой (рантайм). Компилятор **тяжёлый**.

**Решение:** два этапа.
1. **`build`** — берём SDK-образ, компилируем.
2. **`runtime`** — берём **тонкий** рантайм-образ, копируем **только** результат.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "backend.dll"]
```

**Итог:** образ **~200 МБ** вместо **~1.5 ГБ**.

### Docker-сеть

**По умолчанию** контейнеры **изолированы**. Не видят друг друга.

**`docker network create dns-net`** — создаёт **общую** сеть. Внутри неё контейнеры **видят** друг друга **по имени**.

```
dns-backend → dns-mariadb:3306   ← работает
dns-backend → localhost:3306     ← НЕ работает
```

**Docker DNS** — встроенный **резолвер** на `127.0.0.11`. Каждый контейнер в сети может спросить «где `dns-mariadb`?» и получить IP.

### Bind-mount

**Монтирование** папки с **хоста** в **контейнер**. Изменения **в обе стороны**.

```bash
-v /host/data:/container/data
```

**В нашем проекте:**
- `data/db-data` → `/var/lib/mysql` (MariaDB).
- `data/backend` → `/app/data` (SQLite).
- `config/nginx/nginx.conf` → `/etc/nginx/nginx.conf` (конфиг).

**Нюанс:** если монтируешь **файл**, он **должен существовать** на хосте. Иначе Docker создаст **директорию** — и приложение упадёт.

### Права и root

Контейнеры **по умолчанию** работают от **root**. Файлы в bind-mount — **принадлежат root** на хосте. Поэтому с хоста их **нельзя** редактировать **без `sudo`**.

---

## Frontend: React + Vite

### React

**Компоненты** — «кирпичики» UI. Каждый компонент — это **функция**, возвращающая **JSX** (разметку).

```tsx
function Clock() {
  return <div className="clock">12:34:56</div>;
}
```

### JSX

**Синтаксис**, похожий на HTML, **внутри** JavaScript. Компилируется в `React.createElement(...)`.

**Отличия от HTML:**
- `class` → `className`.
- `onclick` → `onClick`.
- Теги **обязательно** закрывать: `<br />`.

### Состояние (state)

**Данные**, которые **меняются** во времени. `useState`:
```tsx
const [count, setCount] = useState(0);
setCount(count + 1);   // → React перерисует компонент
```

### Эффекты (side effects)

**Побочные эффекты** — запросы, таймеры, подписки. `useEffect`:
```tsx
useEffect(() => {
  const timer = setInterval(() => setNow(new Date()), 1000);
  return () => clearInterval(timer);   // cleanup при unmount
}, []);   // [] — выполнить один раз
```

### Vite

**Сборщик.** Делает **три** вещи:
1. **Dev-server** — `npm run dev` на `localhost:5173`, **hot-reload**.
2. **Сборка** — `npm run build`, превращает `src/` в `dist/`.
3. **Оптимизация** — минификация, tree-shaking, хеши.

**`vite.config.ts`** — конфиг. В нём — **proxy** для `/api/` (чтобы dev-server перенаправлял запросы на backend).

### SPA-fallback

**Single Page Application** — одна HTML-страница. Все URL'ы ведут на **неё**. React **сам** решает, что показывать (через роутинг).

**Nginx** должен отдавать `index.html` **для всех** путей:
```nginx
location / {
    try_files $uri $uri/ /index.html;
}
```

Иначе при обновлении страницы `/admin` → `404`.

---

## Backend: ASP.NET Core

### Minimal API

**Стиль** без контроллеров. Эндпоинты — прямо **в** `Program.cs`:
```csharp
app.MapGet("/api/health", () => new { status = "ok" });
app.MapPost("/api/auth/login", (LoginRequest req) => { ... });
```

**Плюсы:** меньше кода, всё **видно**.
**Минусы:** для **больших** проектов — плохо структурируется.

### DI (Dependency Injection)

**Автоматическая подстановка** зависимостей. Ты **регистрируешь** сервисы:
```csharp
builder.Services.AddSingleton<SqliteContext>();
builder.Services.AddScoped<MariaDbContext>();
```

**Потом** просто **просишь** их в параметрах эндпоинта:
```csharp
group.MapPost("/register", async (RegisterRequest req, MariaDbContext db, ...) => { ... });
```

ASP.NET **сам** создаст и передаст.

### Виды lifetime

| Lifetime | Когда создаётся | Когда удаляется |
|---|---|---|
| **Singleton** | Один раз на приложение | При завершении |
| **Scoped** | Один раз на запрос | В конце запроса |
| **Transient** | Каждый раз при запросе | В конце запроса |

**Правило:** если **зависит** от scoped — **не может** быть singleton.

### EF Core

**ORM** — Object-Relational Mapping. Позволяет работать с БД **как с объектами C#**:
```csharp
var user = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin");
```

EF Core **сам** сгенерирует SQL:
```sql
SELECT * FROM users WHERE username = 'admin' LIMIT 1;
```

**Плюсы:** не пишешь SQL руками.
**Минусы:** сложные запросы — **тяжело** писать, могут быть **медленными**.

### Fluent API

**Конфигурация** моделей в `OnModelCreating`:
```csharp
modelBuilder.Entity<User>(entity =>
{
    entity.ToTable("users");
    entity.Property(e => e.Id).HasColumnName("id").HasColumnType("char(36)");
});
```

**Зачем:** если **имена** в C# (`Id`, `Username`) **не совпадают** с именами в БД (`id`, `username`).

### BackgroundService

**Фоновый** сервис, работает **параллельно** с приложением:
```csharp
public class OutboxSyncService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync();
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

**Регистрация:** `builder.Services.AddHostedService<OutboxSyncService>();` — **до** `builder.Build()`.

**Зачем:** синхронизация outbox → MariaDB. Работает **в фоне**, не блокирует запросы.

### HttpResponse / HttpContext

**Контекст** запроса. Содержит:
- `HttpContext.Request` — что **пришло** (метод, URL, заголовки, тело).
- `HttpContext.Response` — что **отправим** (статус, заголовки, тело).
- `HttpContext.Items` — **словарь** для передачи данных **между** middleware и эндпоинтом.
- `HttpContext.Request.Cookies` — **cookies** из запроса.

### Result types

**Minimal API** возвращает **результаты**:
- `Results.Ok(data)` → 200.
- `Results.BadRequest(error)` → 400.
- `Results.Unauthorized()` → 401.
- `Results.Conflict(error)` → 409.
- `Results.Accepted(uri, data)` → 202.

---

## Что **не** объяснено здесь

- **Nginx**: конфигурация, `resolver`, `proxy_pass`.
- **SQLite**: WAL-режим, `PRAGMA`.
- **BCrypt**: work factor, соль.
- **Dockerfile**: слои, кеш, `.dockerignore`.

Эти темы — в **третьем** файле (`TROUBLESHOOTING.md`) — с **реальными** проблемами и их решениями.

---

## 📖 Полезные ссылки

- [React docs](https://react.dev)
- [Vite docs](https://vitejs.dev)
- [ASP.NET Core docs](https://learn.microsoft.com/aspnet/core)
- [EF Core docs](https://learn.microsoft.com/ef/core)
- [Docker docs](https://docs.docker.com)
- [Nginx docs](https://nginx.org/en/docs)
