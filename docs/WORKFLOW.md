# Рабочий процесс

Практическая инструкция по ежедневной работе с `stathub.server`. Все команды выполняются
из корня репозитория (`stathub.server`), кроме отмеченных случаев.

---
dotnet run --project src/Stathub.WebApi --launch-profile http --urls "http://0.0.0.0:5248"

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

### Про `NU1900`

```
error NU1900: Предупреждение как ошибка: Произошла ошибка при получении данных
уязвимости пакета: The SSL connection could not be established
```

Это NuGet-audit не может скачать индекс уязвимостей — бывает без доступа в интернет
или за прокси с подменой сертификата. Из-за `TreatWarningsAsErrors=true` предупреждение
становится ошибкой и роняет restore всего решения.

**Уже исправлено**: в `Directory.Build.props` добавлено `<NuGetAudit>false</NuGetAudit>`,
так что дополнительных флагов не требуется. Если понадобится вернуть аудит (например,
в CI с нормальным доступом в сеть), уберите эту строку — либо переопределите на разовый
запуск через `-p:NuGetAudit=true`.

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
| `NU1900` при restore | нет сети или прокси с подменой сертификата, NuGet-audit не может скачать индекс уязвимостей | уже исправлено: `<NuGetAudit>false</NuGetAudit>` в `Directory.Build.props` (см. раздел 1) |
| 500 + лавина `Cannot open log for source '.NET Runtime'` | провайдер EventLog не может писать в журнал Windows, и любой `LogWarning` роняет запрос | уже исправлено: `Logging:EventLog:LogLevel:Default=None` в `appsettings.json` |
| `Failed to determine the https port for redirect` | `UseHttpsRedirection()` в Development, где слушается http | уже исправлено: редирект только вне Development |

---

## 8. Поиск игроков: почему реализован именно так

Триграммный поиск в `PlayerRepository.FindByNameAsync` устроен неочевидно, и это
результат замеров, а не вкуса. Если будете трогать — прочитайте сначала.

**Порог сравнивается явно, а не оператором.** Используется
`word_similarity(query, "FullName") >= 0.3`, а не оператор `<%`. Причина: поведение
операторов завязано на сессионный параметр `word_similarity_threshold`, значение
которого неочевидно, а на реальных данных `<%` не находил даже «иваан» → «Иванов».
Явное сравнение делает порог видимым.

**Порядок аргументов принципиален:**

| Выражение | Результат для «сидаров» → «Сидоров Алексей» |
|---|---|
| `word_similarity('сидаров', "FullName")` | **0.455** → находит |
| `word_similarity("FullName", 'сидаров')` | 0.263 → не находит |
| `similarity("FullName", 'сидаров')` (вся строка) | 0.263 → не находит |

Word-level берётся вместо сравнения со всей строкой потому, что последнее «размывается»
длиной ФИО: чем длиннее ФИО, тем ниже схожесть при той же опечатке.

**`ILIKE` добавлен намеренно.** При вводе 2–3 символов («Ку», «сид») схожесть ещё ниже
порога, и триграммы сами по себе не нашли бы ничего — а для typeahead это основной режим.
Без `ILIKE` поиск по началу фамилии не работает.

**Известное ограничение по производительности.** Комбинация
`word_similarity(...) >= 0.3 OR ILIKE` через `OR` не даёт планировщику ни использовать
GIN-индекс, ни применить early-exit по `LIMIT` — фильтр вычисляется для каждой строки.
Замер на 50 000 игроков: **~525 мс**. Для любительской лиги (сотни–тысячи игроков) это
доли миллисекунды, поэтому сейчас приемлемо.

Если объёмы вырастут, оптимизация — разнести две ветки через `UNION` вместо `OR`:
тогда каждая ветка получает свой план. Триграммная ветка при этом должна использовать
оператор `%` или `%>`, а не явное сравнение: замер подтвердил, что GIN-индекс
(`IX_players_FullName`, `gin_trgm_ops`) реально работает с ними — `Bitmap Index Scan`
при `enable_seqscan=off`, 1.9 мс против 3.2 мс. Но учтите: `%>` имеет порог 0.5 и
опечатку «СидароФ» уже не находит (0 совпадений), поэтому для устойчивости к опечаткам
нужен `%` либо явное сравнение.

---

## 9. Что сделано и что дальше

Реализовано:

- **Leagues** — лиги, турниры, этапы с правилами матча.
- **Matches** — создание матча, снимок правил этапа, периоды, старт/пауза/возобновление/
  завершение.
- **Players** — глобальный реестр игроков (без `tenant_id`, см. `doc.md` §2, §9.3) и
  поиск-подсказки, устойчивый к опечаткам.

Дальше по плану:

1. **Identity + Tenancy** — сейчас `organizerId` приходит в теле запроса, то есть любой
   может создать лигу от чужого имени. Без этого админку лиги подключать нельзя.
   Заодно откроется запись игроков: сейчас у Players только чтение и поиск.
2. **Teams** → затем `PlayerRegistration` и `TournamentTeam`. В `PlayerSuggestionDto`
   для этого уже зарезервировано место под контекст последнего участия (команда, лига),
   который `doc.md` §9.3 требует показывать в подсказках.
3. **MatchEvent + офлайн-синхронизация** — ядро killer-фичи (`doc.md` §3, §9):
   приём батча событий, дедупликация по client-generated UUID, `GET /matches/{id}/state`.
4. **SignalR** — live-обновления для зрителей.

Подробности архитектуры: `documentation/doc.md`, `docs/Архитектура_v1.0.md`.
