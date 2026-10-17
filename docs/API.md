# Эндпоинты WebApi — текущее состояние

Снято живой проверкой на `2026-10-11` (Postgres `stathub` на `localhost:5432`, dev-окружение).
Колонка «Проверено» — реальный ответ, полученный от запущенного приложения, а не предположение.

## Как запустить и куда стучаться

```bash
dotnet run --project src/Stathub.WebApi
```

Профиль `http` из `launchSettings.json` слушает **http://localhost:5248**.
`https`-профиль — `https://localhost:7079` и `http://localhost:5248`.

Строка подключения лежит в user secrets (`ConnectionStrings:Postgres`), в `appsettings.json` её нет.

> ⚠️ В dev-окружении `app.UseHttpsRedirection()` включён, но HTTPS-порт не выводится из конфигурации
> при запуске без профиля. В лог пишется предупреждение `Failed to determine the https port for redirect`,
> сам редирект не срабатывает. Для отладки удобнее запускать `http`-профиль.

Документация API в Development: `MapOpenApi()` (документ отдаётся по `/openapi/{documentName}.json`,
имя документа по умолчанию — `v1`) и UI Scalar на **`/scalar/v1`**.

Все enum'ы сериализуются **строками** (`JsonStringEnumConverter`), camelCase по умолчанию:
`sport: "Football"`, `dataSource: "Native"`, `formatType: "Round"`, `status: "Published"`.

## Сводка

| Всего | Работает | Сломано (нет миграции) |
|---|---|---|
| 22 | 15 | 7 (все `/matches*`) |

Раскладка: Leagues — 8, Tournaments/Stages — 7, Matches — 7.

**Ключевое: весь модуль `Matches` сейчас отдаёт 500.** Причина — миграция с новыми колонками
(`CurrentPeriod`, `PeriodsCount`, …) не применена, БД отстала от модели. Подробности в конце.

---

## Leagues — работают полностью

| Метод | Маршрут | Ответ | Проверено |
|---|---|---|---|
| `POST` | `/leagues` | 201 `{id}` | ✅ `DELETE 1`-проверенный цикл создания |
| `GET` | `/leagues` | 200 массив | ✅ 200, вернул данные |
| `GET` | `/leagues?organizerId={guid}` | 200 массив | ✅ 200 (пустой массив при отсутствии) |
| `GET` | `/leagues/{id:guid}` | 200 / 404 | ✅ 404 с `detail` |
| `GET` | `/leagues/by-slug/{slug}` | 200 / 404 | ✅ 200 по реальному slug |
| `POST` | `/leagues/{id:guid}/publish` | 204 / 400 / 404 | ✅ 204 |
| `POST` | `/leagues/{id:guid}/archive` | 204 / 404 | ✅ 204 |

### Тело `POST /leagues`

```json
{
  "organizerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Любительская футбольная лига",
  "slug": "lflperm",
  "sport": "Football",
  "city": "Пермь",
  "region": "Пермский край",
  "dataSource": "Native"
}
```

- `sport`: только `"Football"` (пока единственное значение в `Sport`).
- `dataSource`: `"Native"` | `"Mirrored"`. Необязателен, по умолчанию `"Native"`.
- `city`, `region` — необязательны.

**Поведенческие особенности (важно для UI):**

1. **`GET /leagues` без `organizerId` отдаёт только `Published`.** Это публичная витрина.
   Лиги в статусе `Draft` видны только через `?organizerId=`, и только вместе с остальными
   статусами этого организатора. То есть черновик в публичном списке не появится — это
   задумано (`LeagueService.ListAsync`), но UI админки обязан всегда передавать `organizerId`,
   иначе организатор не увидит свои черновики.
2. **`POST /{id}/publish` вернёт 400, если `city` пустой.** `League.Publish()` требует город:
   без него лига не попадёт в поиск «лиги рядом со мной».
3. **Дубликат slug → 409.** Проверка есть в сервисе, и дополнительно ловится
   `UniqueViolation` в репозитории (защита от гонки).
4. **`by-slug` регистронезависим**: вход приводится к lower case, при создании slug тоже
   нормализуется.
5. **Пагинации нет** — `GET /leagues` отдаёт весь список. На реальных объёмах понадобится.

---

## Tournaments и Stages — работают полностью

Все маршруты вложены в лигу: `/leagues/{leagueId:guid}/tournaments`.

| Метод | Маршрут | Ответ | Проверено |
|---|---|---|---|
| `POST` | `.../tournaments` | 201 `{id}` | ✅ 201 |
| `GET` | `.../tournaments` | 200 массив | ✅ 200 |
| `GET` | `.../tournaments/{tournamentId:guid}` | 200 / 404 | ✅ 200 (со `stages`) |
| `POST` | `.../tournaments/{tournamentId:guid}/stages` | 201 `{id}` | ✅ 201 |
| `POST` | `.../tournaments/{tournamentId:guid}/start` | 204 / 409 / 404 | не проверялся вживую, код симметричен `complete` |
| `POST` | `.../tournaments/{tournamentId:guid}/complete` | 204 / 409 / 404 | ✅ 409 при `Scheduled` |
| `POST` | `.../tournaments/{tournamentId:guid}/cancel` | 204 / 409 / 404 | ✅ 204 |

### Тело `POST .../tournaments`

```json
{ "name": "Кубок города 2026", "startDate": "2026-05-01", "endDate": "2026-08-31" }
```

`endDate` необязателен. Даты — `DateOnly` в формате `yyyy-MM-dd`. Вернётся 400, если `endDate < startDate`.

### Тело `POST .../stages`

```json
{
  "name": "Групповой этап",
  "formatType": "Round",
  "winPoints": 3, "drawPoints": 1, "lossPoints": 0,
  "periodDurationMinutes": 45, "periodsCount": 2,
  "extraTimeEnabled": false, "penaltyShootoutEnabled": false
}
```

- `formatType`: `"Round"` | `"Playoff"`.
- Очки не могут быть отрицательными → 400.
- `periodDurationMinutes` и `periodsCount` должны быть > 0 → 400.

**Поведенческие особенности:**

1. **Этап можно добавить только турниру в статусе `Scheduled`** (`Tournament.AddStage`).
   После `/start` добавление этапа даст 409. Это соответствует `docs/Архитектура_v1.0.md`,
   но для UI означает: состав этапов фиксируется до старта турнира.
2. **Статусы турнира:** `Scheduled` → `Active` (start) → `Completed` (complete).
   `cancel` доступен из любого статуса, кроме `Completed`.
3. **`AddTiebreakerRule` не доступен ни через один эндпоинт.** Метод есть в домене
   (`Stage.AddTiebreakerRule`), но контроллера/поля в запросе для критериев при равенстве очков нет.
   Настроить тай-брейки через API сейчас невозможно.
4. **Пагинации нет.**

---

## Matches — маршруты есть, но падают с 500

| Метод | Маршрут | Фактический ответ | Статус |
|---|---|---|---|
| `POST` | `/matches` | **500** | ❌ нет миграции |
| `GET` | `/matches/{id:guid}` | **500** | ❌ нет миграции |
| `POST` | `/matches/{id:guid}/start` | **500** | ❌ нет миграции |
| `POST` | `/matches/{id:guid}/stop` | **500** | ❌ нет миграции |
| `POST` | `/matches/{id:guid}/resume` | **500** | ❌ нет миграции |
| `POST` | `/matches/{id:guid}/next-period` | **500** | ❌ нет миграции |
| `POST` | `/matches/{id:guid}/finish` | **500** | ❌ нет миграции |

500 — это `Npgsql.PostgresException 42703: столбец m.CurrentPeriod не существует`.
То есть контроллер, DI и сервис **работают**: запрос доходит до БД и падает на схеме.

### Тело `POST /matches`

```json
{
  "stageId": "9bb13469-82be-418d-b820-483ccd8965eb",
  "homeTeamId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "awayTeamId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
  "startTimeUtc": "2026-05-10T12:00:00Z"
}
```

`stageId` должен указывать на существующий этап — иначе 404
`«Правила матча для стадии '…' не найдены»`. `homeTeamId`/`awayTeamId` сейчас **не проверяются
на существование** (модуль Teams пуст), принимается любой непустой GUID.

### Ответ `GET /matches/{id}` (после миграции)

```json
{
  "id": "...", "stageId": "...", "homeTeamId": "...", "awayTeamId": "...",
  "createdAtUtc": "...", "scheduledTimeUtc": "...", "startedAtUtc": null,
  "status": "Scheduled", "homeScore": 0, "awayScore": 0,
  "periodsCount": 2, "periodDurationMinutes": 45,
  "extraTimeEnabled": false, "penaltyShootoutEnabled": false,
  "currentPeriod": 0, "periodStartedAtUtc": null
}
```

`status`: `"Scheduled"` | `"InProgress"` | `"Pause"` | `"Finished"` | `"Cancelled"`.
`currentPeriod = 0` означает, что матч ещё не начат.

---

## Что мешает прямо сейчас

Применить миграцию для модуля Matches:

```bash
dotnet ef migrations add MatchPeriodsAndFormatSnapshot \
  --project src/Modules/Stathub.Modules.Matches/Stathub.Modules.Matches.Infrastructure \
  --startup-project src/Stathub.WebApi \
  --context MatchesDbContext

dotnet ef database update \
  --project src/Modules/Stathub.Modules.Matches/Stathub.Modules.Matches.Infrastructure \
  --startup-project src/Stathub.WebApi \
  --context MatchesDbContext
```

Текущее состояние схемы (проверено в БД): в `matches.matches` 10 колонок —
`Id, StageId, HomeTeamId, AwayTeamId, CreatedAtUtc, ScheduledTimeUtc, StartedAtUtc, Status,
HomeScore, AwayScore`. Модель ожидает ещё 6.

Строки в `matches.matches` существовать не могут: до правки `StartedAtUtc` был `NOT NULL`,
а домен его не заполнял при создании. Значит `NOT NULL`-колонки добавятся без backfill.

## Чего в API ещё нет

- Ни одного эндпоинта для команд и игроков (модули Teams/Identity пусты).
- Ни одного эндпоинта для событий матча (`MatchEvent`) — ядро offline-судейства не начато.
- Ни одного эндпоинта для таблиц/статистики (модуль Standings пуст).
- Аутентификации нет: `organizerId` приходит в теле запроса, любой может создать лигу
  от чужого имени.

---

## Обновление: список матчей этапа

Добавлен маршрут списка матчей — по нему судейский клиент (`stathub.client/apps/mobile`)
выбирает матч для подключения.

| Метод | Маршрут | Ответ | Проверено |
|---|---|---|---|
| `GET` | `/matches?stageId={guid}` | 200 массив `MatchDto` / 400 | ✅ 200 `[]` для существующего этапа; 400 `{"detail":"StageId обязательное поле."}` без `stageId` |

- Матчи сортируются по `scheduledTimeUtc` (ближайший первым), пагинации нет.
- Попутно проверено, что модуль Matches **не падает с 500**: `GET /matches/{id}` возвращает 404
  `«Матч '…' не найден»`, то есть миграция `MatchPeriodsAndFormatSnapshot` применена
  и раздел «Что мешает прямо сейчас» выше устарел.
- Жизненный цикл (`start/stop/resume/next-period/finish`) не перепроверялся: в коде не менялся,
  а для живой проверки понадобился бы реальный матч в БД.
