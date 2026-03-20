# wh40kAPI

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![React](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=white)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5-3178C6?logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![MariaDB](https://img.shields.io/badge/MariaDB-003545?logo=mariadb&logoColor=white)](https://mariadb.org/)
[![Docker](https://img.shields.io/badge/Docker-готов-2496ED?logo=docker&logoColor=white)](https://www.docker.com/)
[![Лицензия: GPL v3](https://img.shields.io/badge/Лицензия-GPLv3-blue.svg)](LICENSE.txt)
[![GitHub stars](https://img.shields.io/github/stars/Shooshpanius/wh40kAPI?style=social)](https://github.com/Shooshpanius/wh40kAPI/stargazers)
[![GitHub issues](https://img.shields.io/github/issues/Shooshpanius/wh40kAPI)](https://github.com/Shooshpanius/wh40kAPI/issues)
[![GitHub последний коммит](https://img.shields.io/github/last-commit/Shooshpanius/wh40kAPI)](https://github.com/Shooshpanius/wh40kAPI/commits)

Full-stack **API для Warhammer 40,000 10-го издания** на базе ASP.NET Core 10 и React 19. Объединяет официальные данные WH40K и сообщественные источники [BSData](https://github.com/BSData) (40K и Kill Team) в единый REST API с интерактивной документацией OpenAPI.

## Возможности

- **Три REST API** — WH40K 10-е издание, BSData 40K и BSData Kill Team — каждый со своей документацией [Scalar](https://scalar.com/) OpenAPI:
  - WH40K API: `/scalar/wh40k`
  - BSData 40K: `/scalar/bsdata`
  - BSData Kill Team: `/scalar/ktbsdata`
- **Панель администратора** на React (`/admin`) — импорт данных Wahapedia и данных сообщества с GitHub
- **Три базы данных MariaDB** — схема создаётся автоматически при первом запуске через EF Core `EnsureCreated`
- **Готов к Docker** — многоэтапные сборки для бэкенда (.NET) и фронтенда (Nginx)

## Архитектура

```
┌─────────────────────────────────────────────────────────────┐
│                        wh40kAPI                             │
├──────────────────────┬──────────────────────────────────────┤
│   Фронтенд (SPA)     │        Бэкенд (ASP.NET Core 10)      │
│   React 19 + Vite    │                                       │
│   TypeScript         │  /api/wh40k/      – официальные      │
│   React Router DOM   │  /api/bsdata/     – BSData 40K       │
│   /admin панель      │  /api/ktbsdata/   – BSData Kill Team │
│   :51018 (дев)       │  :8080 / :8081                       │
└──────────────────────┴────────────────────┬─────────────────┘
                                             │ EF Core + Pomelo
                               ┌─────────────▼─────────────────┐
                               │   MariaDB (3 базы данных)      │
                               │   wh40k          (официальная) │
                               │   wh40kBSData    (40K)         │
                               │   wh40kKTBSData  (Kill Team)   │
                               └───────────────────────────────┘
```

## Источники данных

| API | Источник | Способ импорта |
|-----|----------|----------------|
| WH40K 10-е издание | [Export Data Specs](https://wahapedia.ru/wh40k10ed/Export%20Data%20Specs.xlsx) (wahapedia.ru) | Запуск через панель администратора |
| BSData 40K | [BSData/wh40k-10e](https://github.com/BSData/wh40k-10e) на GitHub | Запуск через панель администратора |
| BSData Kill Team | [BSData/wh40k-killteam](https://github.com/BSData/wh40k-killteam) на GitHub | Запуск через панель администратора |

## Технологический стек

| Слой | Технология |
|------|------------|
| Фреймворк бэкенда | ASP.NET Core 10 |
| ORM | Entity Framework Core 9 + Pomelo (MariaDB/MySQL) |
| Документация API | Scalar (OpenAPI) |
| Фронтенд | React 19 + TypeScript 5 + Vite 7 |
| Маршрутизация | React Router DOM 7 |
| База данных | MariaDB (3 отдельные БД) |
| Разбор Excel / CSV | System.IO.Compression + CsvHelper |
| Контейнеризация | Docker (многоэтапные сборки для бэкенда и фронтенда) |

## Начало работы

### Требования
- .NET 10 SDK
- Node.js 20+
- Сервер **MariaDB** (или совместимый с MySQL)

### Настройка базы данных

Создайте базы данных и отдельного пользователя в MariaDB:

```sql
CREATE DATABASE wh40k CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE wh40kBSData CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE wh40kKTBSData CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'wh40k'@'localhost' IDENTIFIED BY 'ваш_надёжный_пароль';
GRANT ALL PRIVILEGES ON wh40k.* TO 'wh40k'@'localhost';
GRANT ALL PRIVILEGES ON wh40kBSData.* TO 'wh40k'@'localhost';
GRANT ALL PRIVILEGES ON wh40kKTBSData.* TO 'wh40k'@'localhost';
FLUSH PRIVILEGES;
```

### Конфигурация
**Никогда не коммитьте реальные учётные данные в репозиторий.**

#### Вариант 1: Локальный файл настроек (рекомендуется для разработки)

Скопируйте файл-пример и заполните свои значения:

```sh
cp wh40kAPI.Server/appsettings.Development.json.example wh40kAPI.Server/appsettings.Development.json
# Затем отредактируйте appsettings.Development.json — этот файл в .gitignore и не попадёт в коммит
```

#### Вариант 2: .NET User Secrets (только для разработки)

```sh
cd wh40kAPI.Server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost;Port=3306;Database=wh40k;User=wh40k;Password=ваш_реальный_пароль;"
dotnet user-secrets set "AdminAuth:PasswordHash" "ваш_sha256_хеш"
```

#### Вариант 3: Переменные окружения (любое окружение)

```sh
export ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=wh40k;User=wh40k;Password=ваш_реальный_пароль;"
export ConnectionStrings__BsDataConnection="Server=localhost;Port=3306;Database=wh40kBSData;User=wh40k;Password=ваш_реальный_пароль;"
export ConnectionStrings__KtBsDataConnection="Server=localhost;Port=3306;Database=wh40kKTBSData;User=wh40k;Password=ваш_реальный_пароль;"
export AdminAuth__PasswordHash="ваш_sha256_хеш"
```

### Запуск
```sh
cd wh40kAPI.Server
dotnet run
```

Приложение будет доступно по адресу `https://localhost:51018` (Vite dev server).

## Панель администратора

Панель администратора защищена паролем, передаваемым в заголовке `X-Admin-Password`.

Придумайте надёжный пароль, вычислите его SHA-256 хеш и поместите его в `AdminAuth:PasswordHash`
(через `appsettings.Development.json`, user secrets или переменную окружения — **не** напрямую в `appsettings.json`).

> **Важно**: хеш пароля администратора и пароль базы данных — это разные вещи.
> В строках подключения всегда используется **открытый** пароль к БД.
> SHA-256 хеш нужен только для `AdminAuth:PasswordHash`.

Как вычислить хеш пароля:

```sh
# Linux/macOS — скопируйте только hex-строку, без "  -" в конце вывода sha256sum
echo -n "ваш_пароль" | sha256sum | awk '{print $1}'

# PowerShell
[System.Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes("ваш_пароль"))).ToLower()
```

### Смена пароля администратора

1. Вычислите SHA-256 хеш нового пароля (см. выше).
2. Обновите `AdminAuth:PasswordHash` в конфигурации (`appsettings.Development.json`, user secrets или переменная окружения `AdminAuth__PasswordHash`).
3. **Перезапустите сервер**, чтобы изменения вступили в силу.

> **Устранение неполадок: «Сервер недоступен» / ошибки подключения**
>
> Если сервер не отвечает после изменения конфигурации:
> - Убедитесь, что процесс сервера действительно запущен (`dotnet run` или ваш контейнер).
> - Проверьте логи сервера при запуске — неверная строка подключения будет залогирована как предупреждение, сервер запустится, но операции с БД будут падать.
> - Убедитесь, что в поле `Password=` строк подключения указан **открытый** пароль к БД, а не хеш.
> - Убедитесь, что вы редактируете правильный файл конфигурации для вашего окружения (см. [Конфигурация](#конфигурация)).


## Веб-интерфейс (SPA)

Встроенный React-интерфейс доступен по корневому адресу приложения и включает следующие страницы:

| Маршрут | Страница |
|---------|----------|
| `/` | Стартовая страница |
| `/wahapedia` | Главная страница WH40K (обзор данных) |
| `/factions` | Список фракций WH40K |
| `/datasheets` | Датащиты юнитов |
| `/detachments` | Отряды |
| `/stratagems` | Стратагемы |
| `/enhancements` | Улучшения |
| `/bsdata-40k` | Данные BSData 40K |
| `/bsdata-killteam` | Данные BSData Kill Team |
| `/admin` | Панель администратора |

## Эндпоинты API

### WH40K API (`/api/wh40k/`)

| Эндпоинт | Описание |
|---|---|
| `GET /api/wh40k/factions` | Все фракции |
| `GET /api/wh40k/factions/{id}` | Одна фракция |
| `GET /api/wh40k/datasheets?factionId=SM` | Датащиты (фильтр по фракции — необязателен) |
| `GET /api/wh40k/datasheets/{id}` | Один датащит |
| `GET /api/wh40k/datasheets/{id}/abilities` | Способности датащита |
| `GET /api/wh40k/datasheets/{id}/models` | Модели датащита |
| `GET /api/wh40k/datasheets/{id}/wargear` | Снаряжение датащита |
| `GET /api/wh40k/datasheets/{id}/keywords` | Ключевые слова датащита |
| `GET /api/wh40k/datasheets/{id}/unit-composition` | Состав отряда датащита |
| `GET /api/wh40k/datasheets/{id}/options` | Опции датащита |
| `GET /api/wh40k/datasheets/{id}/model-costs` | Стоимости моделей датащита |
| `GET /api/wh40k/abilities?factionId=SM` | Способности (фильтр по фракции — необязателен) |
| `GET /api/wh40k/detachments?factionId=SM` | Отряды (фильтр по фракции — необязателен) |
| `GET /api/wh40k/detachments/{id}` | Один отряд |
| `GET /api/wh40k/detachments/{id}/abilities` | Способности отряда |
| `GET /api/wh40k/strategems?factionId=SM` | Стратагемы (фильтр по фракции — необязателен) |
| `GET /api/wh40k/strategems/{id}` | Одна стратагема |
| `GET /api/wh40k/enhancements?factionId=SM` | Улучшения (фильтр по фракции — необязателен) |
| `GET /api/wh40k/enhancements/{id}` | Одно улучшение |
| `GET /api/wh40k/source` | Книги-источники |
| `GET /api/wh40k/source/{id}` | Одна книга-источник |
| `POST /api/wh40k/admin/import` | Скачать данные с wahapedia.ru и импортировать *(требует заголовок `X-Admin-Password`)* |
| `POST /api/wh40k/admin/verify` | Проверить пароль администратора *(требует заголовок `X-Admin-Password`)* |
| `GET /api/wh40k/admin/status` | Статус базы данных *(требует заголовок `X-Admin-Password`)* |

### BSData WH40K API (`/api/bsdata/`)

| Эндпоинт | Описание |
|---|---|
| `GET /api/bsdata/catalogues` | Все каталоги |
| `GET /api/bsdata/catalogues/{id}` | Один каталог |
| `GET /api/bsdata/catalogues/{id}/units` | Юниты каталога |
| `GET /api/bsdata/catalogues/{id}/rules` | Общие правила/способности каталога |
| `GET /api/bsdata/catalogues/{id}/links` | Зависимости каталога |
| `GET /api/bsdata/fractions` | Все фракции (каталоги с `library=false`) |
| `GET /api/bsdata/fractions/{id}` | Одна фракция |
| `GET /api/bsdata/fractions/{id}/units` | Юниты фракции (рекурсивно через catalogueLinks) |
| `GET /api/bsdata/fractions/{id}/unitsWithCosts` | Юниты фракции со стоимостями |
| `GET /api/bsdata/fractions/{id}/unitsTree` | Дерево юнитов фракции со всеми характеристиками (профили M/T/Sv/W/Ld/OC, оружие Range/A/BS&#124;WS/S/AP/D) и ключевыми словами — достаточно **одного запроса** для отображения каталога фракции |
| `GET /api/bsdata/fractions/{id}/unitsList` | Лёгкое дерево юнитов **без** поля `profiles` и без `infoLinks`/`categories` на дочерних узлах — быстрый список имён/стоимостей для мгновенного отображения каталога фракции |
| `GET /api/bsdata/fractions/{id}/detachments` | Отряды фракции |
| `GET /api/bsdata/units?catalogueId={id}` | Все юниты (фильтр по каталогу — необязателен) |
| `GET /api/bsdata/units/{id}` | Один юнит |
| `GET /api/bsdata/units/{id}/profiles` | Профили юнита |
| `GET /api/bsdata/units/{id}/categories` | Категории юнита |
| `GET /api/bsdata/units/{id}/infolinks` | Информационные ссылки юнита (правила, способности и т.д.) |
| `GET /api/bsdata/units/{id}/entrylinks` | Ссылки на вхождения юнита (снаряжение, опции и т.д.) |
| `GET /api/bsdata/units/{id}/constraints` | Ограничения юнита |
| `GET /api/bsdata/units/{id}/modifiergroups` | Группы модификаторов юнита |
| `GET /api/bsdata/units/{id}/cost-tiers` | Ценовые уровни юнита |
| `GET /api/bsdata/units/{id}/fullNode` | Полный узел (`BsDataUnitNode`) одного юнита с профилями характеристик и дочерними upgrade-узлами (оружие) — используется при выборе конкретного юнита в каталоге |
| `POST /api/bsdata/admin/import` | Импорт из BSData/wh40k-10e на GitHub *(требует заголовок `X-Admin-Password`)* |
| `GET /api/bsdata/admin/status` | Статус базы BSData *(требует заголовок `X-Admin-Password`)* |

#### Формат ответа `unitsTree` / `fullNode`

Каждый узел дерева содержит полный набор данных для отображения карточки юнита:

| Поле | Описание |
|------|----------|
| `profiles` | Характеристики юнита (`typeName="Unit"`: M/T/Sv/W/Ld/OC) и оружия (`typeName` содержит `"Weapons"`: Range/A/BS&#124;WS/S/AP/D) |
| `categories` | Все ключевые слова: боевая роль (`primary=true`) и фракционные/специальные теги (`primary=false`) |
| `infoLinks` | Имена способностей и правил юнита (тип `"rule"`) |
| `children` | Дочерние upgrade-узлы (оружие) — каждый со своими `profiles` и `infoLinks` |

#### Формат ответа `unitsList`

Оптимизированный формат для отображения каталога фракции. Отличается от `unitsTree`:

| Узел | Поля |
|------|------|
| Корневые (`depth=0`) | Все поля кроме `profiles`; в `infoLinks` только `type` и `name` (без `id` и `targetId`) |
| Дочерние (`depth≥1`) | `id`, `name`, `entryType`, `hidden`, `modifierGroups`, `minInRoster`, `maxInRoster`, `costTiers`, `children`; поля `infoLinks` и `categories` не включены |

#### Рекомендуемый сценарий загрузки каталога

```
Пользователь выбирает фракцию
    ↓
GET /fractions/{id}/unitsList   → быстро → показываем список отрядов (без характеристик)
    ↓ (пользователь выбирает юнит)
GET /units/{unitId}/fullNode    → быстро → показываем полный датащит
```

Вместо одного тяжёлого запроса `GET /fractions/{id}/unitsTree` (все характеристики всех юнитов сразу).

### BSData Kill Team API (`/api/ktbsdata/`)

| Эндпоинт | Описание |
|---|---|
| `GET /api/ktbsdata/catalogues` | Все каталоги Kill Team |
| `GET /api/ktbsdata/catalogues/{id}` | Один каталог |
| `GET /api/ktbsdata/catalogues/{id}/units` | Юниты каталога |
| `GET /api/ktbsdata/units?catalogueId={id}` | Все юниты (фильтр по каталогу — необязателен) |
| `GET /api/ktbsdata/units/{id}` | Один юнит |
| `GET /api/ktbsdata/units/{id}/profiles` | Профили юнита |
| `POST /api/ktbsdata/admin/import` | Импорт из BSData/wh40k-killteam на GitHub *(требует заголовок `X-Admin-Password`)* |
| `GET /api/ktbsdata/admin/status` | Статус базы Kill Team BSData *(требует заголовок `X-Admin-Password`)* |

## Развёртывание в Docker

Проект содержит отдельные `Dockerfile` для бэкенда и фронтенда. Фронтенд (Nginx) проксирует запросы к `/api`, `/scalar` и `/openapi` на бэкенд-контейнер.

### Сборка образов

```sh
# Бэкенд (из корня репозитория)
docker build -f wh40kAPI.Server/Dockerfile -t wh40k-api-back .

# Фронтенд
docker build -f wh40kapi.client/Dockerfile -t wh40k-api-front wh40kapi.client/
```

### Переменные окружения для бэкенда

При запуске контейнера передайте строки подключения и хеш пароля через переменные окружения:

```sh
docker run -d \
  -e ConnectionStrings__DefaultConnection="Server=db;Port=3306;Database=wh40k;User=wh40k;Password=ваш_пароль;" \
  -e ConnectionStrings__BsDataConnection="Server=db;Port=3306;Database=wh40kBSData;User=wh40k;Password=ваш_пароль;" \
  -e ConnectionStrings__KtBsDataConnection="Server=db;Port=3306;Database=wh40kKTBSData;User=wh40k;Password=ваш_пароль;" \
  -e AdminAuth__PasswordHash="ваш_sha256_хеш" \
  -p 8080:8080 \
  --name back40api \
  wh40k-api-back
```

### Nginx-прокси (фронтенд)

Конфигурация Nginx в `wh40kapi.client/nginx.conf` настроена так, что запросы к `/api`, `/scalar` и `/openapi` передаются на бэкенд-контейнер `back40api:8080`, а все остальные пути обслуживаются как SPA. Убедитесь, что оба контейнера находятся в одной Docker-сети:

```sh
docker network create wh40k-net
docker run -d --network wh40k-net --name back40api ... wh40k-api-back
docker run -d --network wh40k-net -p 80:80 --name front40api wh40k-api-front
```

## Безопасность

- **Хеш пароля администратора** — пароль для доступа к панели администратора никогда не хранится в открытом виде: в конфигурации указывается только SHA-256 хеш.
- **Ограничение частоты запросов (rate limiting)** — эндпоинты `/admin` защищены: не более **10 запросов в минуту** с одного IP-адреса (при отсутствии IP — 1 запрос в минуту). При превышении лимита возвращается `429 Too Many Requests`.
- **Заголовки безопасности** — все HTTP-ответы содержат заголовки: `X-Content-Type-Options`, `X-Frame-Options`, `X-XSS-Protection`, `Referrer-Policy`, `Permissions-Policy`.
- **Конфигурация через переменные окружения** — в production рекомендуется передавать строки подключения и хеш пароля через переменные окружения, не коммитя их в репозиторий.

## Лицензия

Проект распространяется под лицензией [GNU General Public License v3.0](LICENSE.txt).

