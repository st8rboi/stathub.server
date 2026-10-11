# Рабочий процесс

Практическая инструкция по ежедневной работе с `stathub.server`. Все команды выполняются
из корня репозитория (`stathub.server`), кроме отмеченных случаев.

---

## 1. Сборка

```bash
dotnet build Stathub.slnx -m:1
```

**`-m:1` обязателен.** Без него сборка падает с `exit code 1`, при этом в выводе написано
`Ошибок: 0` — без единого сообщения об ошибке. Причина: MSBuild общается со своими
worker-нодами через именованные каналы, которые блокируются песочницей. Отключать `-m`
нельзя — это не про скорость сборки.

Эта же причина ломает `dotnet ef`: он внутри вызывает `dotnet build` **без** `-m:1` и
потом сообщает невнятное `Build failed. Use dotnet build to see the errors.`
Лечится схемой «собрать руками → `--no-build`» (см. раздел 3).

Тестовый проект пока не заведён:

```bash
dotnet test Stathub.slnx -m:1
```

---

## 2. Запуск API

```bash
dotnet run --project src/Stathub.WebApi
```

Подхватывается профиль `http` из `launchSettings.json` → **http://localhost:5248**.

Проверка, что всё живо:

```bash
curl http://localhost:5248/leagues          # 200 + JSON
```

Полезные адреса (только в Development):

| Адрес | Что |
|---|---|
| `/scalar/v1` | UI Scalar — интерактивная документация, удобно дёргать эндпоинты |
| `/openapi/v1.json` | OpenAPI-документ |
| `/leagues` | рабочий публичный список лиг |

`GET /` отдаёт **404** — это нормально, корневой маршрут не зарегистрирован.

Строка подключения к БД лежит в user secrets, а не в `appsettings.json`:

```bash
dotnet user-secrets list --project src/Stathub.WebApi
```

---

## 3. Миграции

Правило из `AGENTS.md`: **миграции запускает человек**, агент только готовит изменения
сущностей и конфигураций.

Из-за блокировки именованных каналов (раздел 1) порядок всегда такой:

```bash
# 1) собрать вручную
dotnet build Stathub.slnx -m:1

# 2) создать миграцию
dotnet ef migrations add <Имя> \
  --project src/Modules/Stathub.Modules.<Module>/Stathub.Modules.<Module>.Infrastructure \
  --startup-project src/Stathub.WebApi \
  --context <Module>DbContext \
  --no-build

# 3) посмотреть, что получилось (важно: проверить SQL глазами)
cat src/Modules/Stathub.Modules.<Module>/Stathub.Modules.<Module>.Infrastructure/**/Migrations/*_<Имя>.cs

# 4) пересобрать (миграция стала частью сборки)
dotnet build Stathub.slnx -m:1

# 5) применить
dotnet ef database update \
  --project src/Modules/Stathub.Modules.<Module>/Stathub.Modules.<Module>.Infrastructure \
  --startup-project src/Stathub.WebApi \
  --context <Module>DbContext \
  --no-build
```

Проверить, что ожидает применения, и не собирать лишнего:

```bash
dotnet build Stathub.slnx -m:1
dotnet ef migrations list \
  --project src/Modules/Stathub.Modules.<Module>/Stathub.Modules.<Module>.Infrastructure \
  --startup-project src/Stathub.WebApi \
  --context <Module>DbContext \
  --no-build
```

Строки вида `20261010083413_Xxx (Pending)` — ещё не применены.

### Особенности, о которых стоит помнить

- **Пути к миграциям у модулей разные:** `Leagues/.../Persistence/Migrations`,
  `Matches/.../Migrations`. Не выравнивать без запроса (`AGENTS.md`).
- **Таблица истории миграций одна на все модули** — `public.__EFMigrationsHistory`,
  а не в схеме модуля. Фактического конфликта нет (ID миграций уникальны, каждый контекст
  считает pending только свои), но это латентный риск. Если модули начнут пересекаться —
  задать `MigrationsHistoryTable` на модуль.
- **Правки в `**/Migrations/*`, `*.Designer.cs`, `*ModelSnapshot.cs` руками не вносить**
  (`AGENTS.md`).

---

## 4. Работа с БД напрямую

Клиент: `C:\Program Files\PostgreSQL\18\bin\psql.exe`, БД `stathub`, пользователь `postgres`.

Из-за регистра в именах колонок (EF создаёт их в PascalCase) запросы удобнее держать
в `.sql`-файле и подавать через `-f` — в PowerShell кавычки съедаются:

```bash
PGPASSWORD=... "/c/Program Files/PostgreSQL/18/bin/psql.exe" \
  -h localhost -p 5432 -U postgres -d stathub -f query.sql
```

Пример `query.sql`:

```sql
SELECT "Slug", "Name", "Status" FROM leagues.leagues ORDER BY "CreatedAtUtc";
SELECT "PeriodsCount", "PeriodDurationMinutes", "CurrentPeriod" FROM matches.matches;
```

Схемы: `leagues`, `matches`. У каждой сущности своя схема, cross-module FK нет
(`AGENTS.md`, правило 3).

---

## 5. Проверка эндпоинтов

Полный инвентарь — в `docs/API.md`. Быстрая проверка живого API:

```bash
curl -s http://localhost:5248/leagues | head -c 400
```

При создании вложенных сущностей помните про порядок: **лига → турнир → этап → матч**.
`stageId` обязателен для матча, иначе 404 «Правила матча для стадии не найдены».

Пример полной цепочки (bash, без кириллицы в теле — иначе проблемы с кодировкой в консоли):

```bash
BASE=http://localhost:5248
ORG=$(uuidgen)

LEAGUE=$(curl -s -X POST $BASE/leagues -H 'Content-Type: application/json' -d "{
  \"organizerId\":\"$ORG\",\"name\":\"Test League\",\"slug\":\"test-$RANDOM\",
  \"sport\":\"Football\",\"city\":\"Perm\",\"dataSource\":\"Native\"}" | jq -r .id)

TOURN=$(curl -s -X POST $BASE/leagues/$LEAGUE/tournaments -H 'Content-Type: application/json' \
  -d '{"name":"Cup","startDate":"2026-05-01"}' | jq -r .id)

STAGE=$(curl -s -X POST $BASE/leagues/$LEAGUE/tournaments/$TOURN/stages \
  -H 'Content-Type: application/json' -d '{
  "name":"Group","formatType":"Round","winPoints":3,"drawPoints":1,"lossPoints":0,
  "periodDurationMinutes":45,"periodsCount":2,
  "extraTimeEnabled":true,"penaltyShootoutEnabled":true}' | jq -r .id)

MATCH=$(curl -s -X POST $BASE/matches -H 'Content-Type: application/json' -d "{
  \"stageId\":\"$STAGE\",\"homeTeamId\":\"$(uuidgen)\",\"awayTeamId\":\"$(uuidgen)\",
  \"startTimeUtc\":\"2026-05-10T12:00:00Z\"}" | jq -r .id)

curl -s -X POST $BASE/matches/$MATCH/start
curl -s $BASE/matches/$MATCH | jq '{status,currentPeriod,periodsCount,periodDurationMinutes}'
```

Ожидаемый ответ после старта: `status=InProgress`, `currentPeriod=1`, а `periodsCount`
и `periodDurationMinutes` должны совпадать с тем, что отправляли в этап.

---

## 6. Уборка тестовых данных

Тестовые записи удобно помечать общим префиксом slug (`test-`, `e2e-`, `probe-`) и удалять
одним запросом — каскад снимет турниры и этапы:

```sql
DELETE FROM leagues.leagues WHERE "Slug" LIKE 'test-%' OR "Slug" LIKE 'e2e-%';
```

Матчи не каскадятся (cross-module FK не делаем), поэтому их удалять отдельно:

```sql
DELETE FROM matches.matches WHERE "StageId" IN (
  SELECT s."Id" FROM leagues.stages s
  JOIN leagues.tournaments t ON t."Id" = s."TournamentId"
  JOIN leagues.leagues l ON l."Id" = t."LeagueId"
  WHERE l."Slug" LIKE 'test-%'
);
```

Порядок важен: сначала матчи, потом лиги.

---

## 7. Известные грабли окружения

| Симптом | Причина | Решение |
|---|---|---|
| Сборка падает, `Ошибок: 0` | MSBuild worker-ноды через именованные каналы | добавить `-m:1` |
| `dotnet ef` → `Build failed` | EF вызывает `dotnet build` без `-m:1` | собрать руками, затем `--no-build` |
| `NU1900` при restore нового проекта | нет сети, NuGet-audit не может скачать индекс уязвимостей | `<NuGetAudit>false</NuGetAudit>` в проекте |
| 500 + лавина `Cannot open log for source '.NET Runtime'` | провайдер EventLog не может писать в журнал Windows, и любой `LogWarning` роняет запрос | уже исправлено: `Logging:EventLog:LogLevel:Default=None` в `appsettings.json` |
| `Failed to determine the https port for redirect` | `UseHttpsRedirection()` в Development, где слушается http | уже исправлено: редирект только вне Development |

---

## 8. Что сделано и что дальше

Реализовано: модули **Leagues** (лиги, турниры, этапы с правилами) и **Matches**
(создание матча, снимок правил, периоды, старт/пауза/возобновление/завершение).

Дальше по плану:

1. **Identity + Tenancy** — сейчас `organizerId` приходит в теле запроса, то есть любой
   может создать лигу от чужого имени. Без этого админку лиги подключать нельзя.
2. **Teams / Players** — `homeTeamId`/`awayTeamId` пока не проверяются на существование,
   модуль пуст.
3. **MatchEvent + офлайн-синхронизация** — ядро killer-фичи (`doc.md` §3, §9):
   приём батча событий, дедупликация по client-generated UUID, `GET /matches/{id}/state`.
4. **SignalR** — live-обновления для зрителей.

Подробности архитектуры: `documentation/doc.md`, `docs/Архитектура_v1.0.md`.
