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

---

## Обновление: протокол матча (события)

Лог событий — источник истины, счёт (`homeScore`/`awayScore`) — его проекция.
Идентификатор события генерирует клиент, поэтому повторная отправка офлайн-очереди идемпотентна.

| Метод | Маршрут | Ответ | Проверено |
|---|---|---|---|
| `GET` | `/matches/{id:guid}/state` | 200 `{ match, events }` / 404 | ✅ 200, события в порядке `sequenceNumber` |
| `POST` | `/matches/{id:guid}/events` | 200 `{ acceptedEventIds, duplicateEventIds, state }` / 400 / 404 | ✅ см. ниже |

### Тело `POST /matches/{id}/events`

```json
{
  "events": [
    {
      "id": "0f1c6a2e-1b77-4a1e-9d3a-5c2f0b8e4a11",
      "type": "Goal",
      "teamId": "11111111-1111-1111-1111-111111111111",
      "playerId": null,
      "relatedPlayerId": null,
      "targetEventId": null,
      "reason": null,
      "clientTimestampUtc": "2026-10-17T12:00:00Z",
      "sequenceNumber": 1
    }
  ]
}
```

- `type`: `Goal` | `OwnGoal` | `YellowCard` | `RedCard` | `Substitution` | `EventVoided`.
- `sequenceNumber` — порядок внутри матча, назначает клиент (должен быть > 0).
- Отмена — событие `EventVoided` с `targetEventId`; отменить событие отмены нельзя.
- `teamId` для `OwnGoal` — команда игрока, забившего в свои ворота: гол засчитывается сопернику.
- `playerId` / `relatedPlayerId` — игрок и второй игрок (ассистент гола, выходящий на замену).
  Модуля заявок (`PlayerRegistration`) пока нет, проверка существования игрока не выполняется.

### Проверено живьём

- гол + автогол хозяев → `1:1`;
- отмена гола → `0:1`, у отменённого события `isVoided: true`;
- повторная отправка того же батча → `acceptedEventIds: []`, `duplicateEventIds: [2]`, счёт не меняется;
- гол за команду, не играющую в матче → 400; отмена несуществующего события → 400;
- пустой список событий → 400; событие с `sequenceNumber: 0` → 400;
- событие после `/finish` → 400 («протокол можно менять только до финального свистка»);
- несуществующий матч → 404.

---

## Обновление: команды и игроки

Справочники для админки: организатор заводит клуб и игрока один раз, а матчи и заявки
ссылаются на их Id. Обе сущности — «идентичности» без TenantId (documentation/doc.md §7).

| Метод | Маршрут | Ответ | Проверено |
|---|---|---|---|
| `POST` | `/teams` | 201 `{id}` / 400 | ✅ 201, `{"name":"ФК Тестовая","city":"Пермь"}` |
| `GET` | `/teams?query={текст}&limit=` | 200 массив | ✅ поиск по названию (регистронезависимый) |
| `GET` | `/teams/{id:guid}` | 200 / 404 | ✅ |
| `POST` | `/players` | 201 `{id}` / 400 | ✅ 201, `{"fullName":"Иванов Иван Иванович","birthDate":"2000-01-15"}` |
| `GET` | `/players?query={ФИО}&limit=` | 200 массив | ✅ без `query` — недавно добавленные, с `query` — поиск с учётом опечаток |

- `GET /players` без `query` отдаёт последних добавленных (`DefaultListLimit = 20`),
  с `query` — тот же триграммный поиск, что и в `/players/suggest`.
- Дубликаты по ФИО не блокируются: полные тёзки — норма, решение принимает человек (§9.3).
- Проверенные ошибки: пустое название команды → 400 `«Название команды обязательное поле.»`,
  пустое ФИО → 400 `«ФИО игрока обязательное поле.»`.
- Сквозная проверка: матч, созданный с Id команды из `/teams`, принимается `POST /matches` → 201.

---

## Обновление: заявки команд и игроков (сезон)

Новый модуль `Stathub.Modules.Rosters`. Заявки живут на уровне **турнира (сезона)**, а не этапа.
`TournamentTeam` ссылается на турнир и команду, `PlayerRegistration` — на заявку команды и игрока;
ссылки — обычные `Guid`, существование проверяется читателями (`ITournamentReader`, `ITeamReader`,
`IPlayerReader`), потому что межмодульные FK запрещены.

| Метод | Маршрут | Ответ |
|---|---|---|
| `POST` | `/tournaments/{tournamentId}/teams` | 201 `{id}` / 404 / 409 |
| `GET` | `/tournaments/{tournamentId}/teams` | 200 массив (с `activePlayers`) |
| `GET` | `/tournaments/{tournamentId}/teams/{id}` | 200 с составом / 404 |
| `POST` | `/tournaments/{tournamentId}/teams/{id}/players` | 200 `{id}` / 404 / 409 |
| `POST` | `/tournaments/{tournamentId}/teams/{id}/players/{registrationId}/release` | 204 / 409 |
| `POST` | `/tournaments/{tournamentId}/transfers` | 204 / 404 / 409 |
| `POST` | `/tournaments/{tournamentId}/teams/{id}/withdraw` | 204 / 409 |

Тела:

```json
// POST .../teams
{ "teamId": "d7014a32-aa30-45b3-b104-8374960c511e" }

// POST .../teams/{id}/players
{ "playerId": "eb6523e6-7845-4bb3-9b7c-db7ede0adaca", "squadNumber": 7 }

// POST .../transfers
{ "playerId": "eb6523e6-...", "fromTeamId": "d7014a32-...", "toTeamId": "daecada9-...", "squadNumber": 9 }
```

Правила (проверены живьём):

- одна команда — одна заявка на турнир (повтор → 409 `«Команда уже заявлена в этом турнире.»`);
- игрок — не больше одной активной заявки в турнире: повторная заявка в другую команду → 409
  `«Игрок уже заявлен за другую команду этого турнира — оформите трансфер…»`;
- несуществующий игрок/команда → 404;
- трансфер закрывает старую заявку (`TransferredOut` + `LeftOn`) и открывает новую (`Active`);
- снятие команды (`withdraw`) закрывает все её активные заявки и ставит `Withdrawn`;
- заявки недоступны, если турнир `Completed`/`Cancelled` (409).

Дополнительно у игроков появился `GET /players?ids=guid1,guid2` — чтобы показывать ФИО в составах,
не зная ничего о таблице игроков.

---

## Обновление: публичная витрина и таблица (doc §2.1, §4, §9.2)

Read-only маршруты без аутентификации; черновики и архивные лиги не отдаются.

| Метод | Маршрут | Ответ |
|---|---|---|
| `GET` | `/public/leagues?query=&city=` | 200 массив опубликованных лиг |
| `GET` | `/public/leagues/{slug}` | 200 лига + турниры + этапы / 404 |
| `GET` | `/public/stages/{id}` | 200 этап + матчи + **таблица** / 404 |
| `GET` | `/public/matches/{id}` | 200 счёт + протокол с именами команд и игроков / 404 |
| `GET` | `/public/live` | 200 матчи в статусе `InProgress`/`Pause` |

**Таблица** считается на каждый GET из завершённых матчей этапа (никакого кэша, §9.2):
очки из правил этапа, затем тай-брейки в заданном приоритете (`HeadToHead`,
`GoalDifference`, `GoalsScored`), при полном равенстве — стабильный порядок по Id команды.
Модуль `Stathub.Modules.Standings` — чистое чтение, без своего DbContext и миграций.

**SignalR (однонаправленный, §4):**

- хаб `/hubs/matches`, клиент вызывает `SubscribeMatch(matchId)` или `SubscribeLive()`;
- сервер шлёт `MatchUpdated(matchId)` в группу `match:{id}` и `LiveFeedChanged(matchId)`
  в группу `live` после каждого действия судьи (события и смены статуса);
- сообщение — только сигнал перечитать REST-состояние; источник истины остаётся за
  `GET /matches/{id}/state`, поэтому пропущенный пуш ничего не ломает;
- Redis-backplane не включён: хаб работает в пределах одного инстанса (для нескольких
  реплик потребуется `Microsoft.AspNetCore.SignalR.StackExchangeRedis` и Redis).

Проверено живьём: каталог отдаёт опубликованную лигу; страница лиги — турнир и этап;
таблица на реальных матчах (`ФК Тест-А` 3 очка за победу 1:0, `ФК Тест-Б` 0);
публичная страница матча отдаёт названия команд, лиги, турнира и этапа; `negotiate`
хаба возвращает `connectionId` и WebSocket-транспорт.

---

## Обновление: логотипы и расписание по дням

**Логотипы** (команды и лиги). Файл кладётся в хранилище, в БД — только путь; домен не знает
о способе хранения, поэтому MinIO подключается заменой `ILogoStorage` (doc §5).

| Метод | Маршрут | Ответ |
|---|---|---|
| `POST` | `/teams/{id}/logo` | 200 `TeamDto` / 400 / 404 |
| `DELETE` | `/teams/{id}/logo` | 200 `TeamDto` (логотип сброшен) |
| `POST` | `/leagues/{id}/logo` | 200 `LeagueDto` / 400 / 404 |
| `DELETE` | `/leagues/{id}/logo` | 200 `LeagueDto` |

- Тело — `multipart/form-data` с полем `file`; PNG/JPEG/WebP, до 2 МБ.
- Файл отдаётся статикой по `/uploads/logos/{guid}.{ext}` (dev-хранилище — `wwwroot/uploads`,
  каталог в `.gitignore`).
- В ответах появились `logoUrl`: у команды, лиги, строки таблицы (`teamLogoUrl`) и матча
  (`homeTeamLogoUrl`, `awayTeamLogoUrl`, `leagueLogoUrl`).
- Проверено: загрузка → 200 с путём; повторное чтение из БД отдаёт тот же путь; статика — `200 image/png`;
  логотип виден в таблице этапа и на публичной странице матча.
- Замечание по реализации: чтение для изменения идёт через `GetForUpdateAsync` (без `AsNoTracking`),
  иначе EF не замечает правку и `SaveChanges` ничего не пишет.

**Расписание по дням** для полосы дат и списка соревнований:

| Метод | Маршрут | Ответ |
|---|---|---|
| `GET` | `/public/matches?fromUtc=&toUtc=` | 200 матчи в интервале `[fromUtc, toUtc)` |

Клиент запрашивает неделю одним запросом и сам группирует матчи по локальным дням и лигам —
это дешевле, чем отдельный эндпоинт счётчиков на каждый день.

Проверено: 9 матчей за день, названия и логотипы подтягиваются; матчи с неизвестным этапом
(тестовые данные) отдаются с пустыми названиями лиги/этапа — витрина такие в списке пропускает.

---

## Обновление: статистика игроков по турниру

| Метод | Маршрут | Ответ |
|---|---|---|
| `GET` | `/public/tournaments/{id}/statistics` | 200 `{ tournament, rows }` / 404 (нет турнира или лига не опубликована) |

`rows` — по строке на игрока, отсортировано по голам, затем по числу матчей:

| Поле | Смысл |
|---|---|
| `goals` / `ownGoals` | голы и автоголы (отменённые события не считаются) |
| `assists` | ассисты: `RelatedPlayerId` гола (второй игрок события) |
| `yellowCards` / `redCards` | карточки |
| `matches` | матчи турнира, где у игрока было хотя бы одно событие |
| `teamName` / `teamLogoUrl` | последняя известная команда игрока в этом турнире |
| `playerName` | «Фамилия Имя» без отчества — витрина показывает имя одной строкой |

Отдельного события «ассист» в протоколе нет: ассистент — это `RelatedPlayerId` события `Goal`
(валидация «ассистент ≠ автор» уже в домене). Витрина строит из одного ответа пять «топов»:
бомбардиры (`goals`), ассистенты (`assists`), гол+пас (`goals + assists`), ЖК и КК —
сортировка по соответствующей метрике, нулевые строки в раздел не попадают.

Судья отмечает ассистента в мобильном приложении: гол → команда → автор → ассистент
(шаг можно пропустить). Карточка: команда → игрок → тип (игрок тоже необязателен).

Реализация — отдельный модуль `Stathub.Modules.Statistics` (read-only, без своего `DbContext`
и миграций): домен считает из событий, порты читают их из Matches, ФИО — из Players,
команды — из Teams. События без `playerId` (гол «без автора», карточка команды) в личную
статистику не попадают; `EventVoided` исключает цель из подсчёта.

Проверено живьём: гол + отменённый гол + ЖК + КК дали `Г=1` (отменённый не засчитан),
`ЖК=1`, `КК=1`, у обоих игроков `М=1`, названия и логотипы команд подтянулись.

