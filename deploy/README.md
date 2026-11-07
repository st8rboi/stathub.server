# Деплой Stathub на VPS

Один сервер, Docker Compose: `nginx` (обе SPA + прокси в API) → `api` (ASP.NET) → `postgres`.
Наружу открыт только nginx (порты 80 и 443): витрина на `https://статхаб.рф/`, админка на `https://admin.статхаб.рф/`.
HTTP и заход по IP редиректят на HTTPS. В конфигах домены записаны в punycode: `статхаб.рф` = `xn--80aad5dfbv.xn--p1ai`.

Образы и фронт собираются **локально** (на сервере 2 ГБ RAM — сборка .NET там мучительна),
на сервер копируются готовые артефакты.

Ниже `IP` — адрес сервера (`91.206.92.28`), пользователь — `shadmin`, каталог на сервере — `~/stathub`.

---

## Первый запуск

### 1. Локально: собрать образ API

Docker Desktop должен быть запущен. Из каталога `stathub.server`:

```powershell
docker build --platform linux/amd64 -t stathub-api:latest .
docker save -o stathub-api.tar stathub-api:latest
```

### 2. Локально: собрать фронт

Из каталога `stathub.client`:

```powershell
pnpm install
pnpm build:public
pnpm build:web
```

Результат: `apps/public/dist` (витрина) и `apps/web/dist` (админка).

### 3. Скопировать всё на сервер

Из каталога `stathub.server`:

```powershell
scp -r deploy shadmin@IP:stathub
scp stathub-api.tar shadmin@IP:/tmp/
scp -r ..\stathub.client\apps\public\dist shadmin@IP:/tmp/public
scp -r ..\stathub.client\apps\web\dist shadmin@IP:/tmp/admin
```

### 4. На сервере: загрузить образ, разложить фронт, создать .env

```bash
cd ~/stathub
docker load -i /tmp/stathub-api.tar && rm /tmp/stathub-api.tar

mkdir -p sites
mv /tmp/public sites/public
mv /tmp/admin sites/admin

cp .env.example .env
chmod 600 .env
openssl rand -hex 24   # → POSTGRES_PASSWORD
openssl rand -hex 32   # → JWT_KEY
nano .env              # вписать оба значения

chmod +x backup.sh
bash ssl.sh ensure   # временный сертификат, без него nginx не стартует
```

### 5. Запустить

```bash
docker compose up -d
docker compose ps          # все три сервиса в состоянии running / healthy
docker compose logs -f api # миграции применяются при старте; Ctrl+C — выйти из логов
```

Выпустить настоящий сертификат (DNS всех доменов уже должен указывать на сервер, см. раздел «Домен и HTTPS»):

```bash
bash ssl.sh issue
```

Проверка: `https://статхаб.рф/` — витрина, `https://admin.статхаб.рф/` — админка.

### 6. Создать первую организацию и организатора

Публичной регистрации нет, первый аккаунт создаётся командой:

```bash
docker compose run --rm api create-organizer \
  --tenant "Название лиги" --login owner --password 'Надёжный-пароль' --name "Имя Фамилия"
```

После этого — вход в админку под `owner`. Команда попадёт в историю shell; при желании
очистите её: `history -d $(history 1)`.

### 7. Бэкапы

```bash
mkdir -p ~/stathub/backups
./backup.sh            # проверить вручную
crontab -e             # добавить строку:
# 30 3 * * * /home/shadmin/stathub/backup.sh >> /home/shadmin/stathub/backups/backup.log 2>&1
```

Бэкапы на этом же сервере не спасут при его потере — периодически забирайте их к себе:

```powershell
scp -r shadmin@IP:stathub/backups .\stathub-backups
```

Восстановление БД из дампа:

```bash
docker compose exec -T postgres pg_restore -U stathub -d stathub --clean --if-exists < backups/db_ДАТА.dump
```

---

## CI/CD (GitHub Actions)

После первого запуска (шаги выше) обновления идут автоматически:

| Репозиторий | На PR в `main` | После merge в `main` |
|---|---|---|
| `stathub.server` | build + тесты (с Postgres) | + `dotnet publish` → rsync в `~/stathub/api/publish` (только изменившиеся файлы) → `docker build` на сервере по `api/Dockerfile` → `docker compose up -d` → проверка, что API ответил |
| `stathub.client` | lint + build админки и витрины | + выкладка `dist` в `sites/admin` и `sites/public` |

Workflow: `stathub.server/.github/workflows/ci-cd.yml`, `stathub.client/.github/workflows/ci-cd.yml`.
Конфиги из `deploy/` (compose, nginx, backup.sh) тоже синхронизируются при деплое сервера;
`.env`, `sites/`, `backups/` на сервере не трогаются.

### Настройка (один раз)

1. **Отдельный ключ для CI** (не ваш личный). Локально:

   ```powershell
   ssh-keygen -t ed25519 -f stathub-deploy -C github-actions -N '""'
   type stathub-deploy.pub
   ```

   Строку из `.pub` добавить на сервере в конец `~/.ssh/authorized_keys` (`nano ~/.ssh/authorized_keys`).

2. **Отпечаток сервера**, чтобы CI не подключился к подменённому хосту:

   ```powershell
   ssh-keyscan -t ed25519 IP
   ```

3. **rsync на сервере** (нужен для синхронизации): `sudo apt install -y rsync`.

4. **Секреты** — в *обоих* репозиториях: Settings → Secrets and variables → Actions → New repository secret:

   | Секрет | Значение |
   |---|---|
   | `DEPLOY_HOST` | IP сервера |
   | `DEPLOY_USER` | `shadmin` |
   | `DEPLOY_SSH_KEY` | всё содержимое файла `stathub-deploy` (приватный, без `.pub`) |
   | `DEPLOY_KNOWN_HOSTS` | вывод `ssh-keyscan` из п. 2 |

   После этого удалите локальные `stathub-deploy` и `stathub-deploy.pub`.

5. Рекомендуется защитить `main` (Settings → Branches): merge только через PR с зелёной сборкой.

### Откат API

На сервере хранятся 5 последних образов с тегом = SHA коммита:

```bash
docker images stathub-api                         # список
docker tag stathub-api:<SHA> stathub-api:latest
docker compose up -d api
```

Следующий merge в `main` снова выложит свежую версию.

---

## Ручное обновление (без CI)

**API** — шаги 1 и 3 (только `stathub-api.tar`), затем на сервере:

```bash
docker load -i /tmp/stathub-api.tar && rm /tmp/stathub-api.tar
docker compose up -d api
```

Для возможности отката тегируйте сборки датой (`-t stathub-api:2026-11-07`) и указывайте тег
в `API_IMAGE` в `.env`; откат — вернуть прежний тег и `docker compose up -d api`.

**Фронт** — шаги 2 и 3 (только нужный `dist`), затем на сервере, например для админки:

```bash
cd ~/stathub
rm -rf sites/admin && mv /tmp/admin sites/admin
```

Перезапуск nginx не нужен.

**Конфиги** (`docker-compose.yml`, `nginx/`) — скопировать изменённый файл через `scp`, затем
`docker compose up -d` (для nginx-конфигов: `docker compose exec nginx nginx -s reload`).

---

## Домен и HTTPS

Домен `статхаб.рф` в reg.ru, DNS — бесплатные `ns1.reg.ru` / `ns2.reg.ru`. Записи зоны:

| Тип | Имя | Значение |
|---|---|---|
| A | `@` | `91.206.92.28` |
| A | `www` | `91.206.92.28` |
| A | `admin` | `91.206.92.28` |

AAAA-записей быть не должно (у сервера нет IPv6 в конфиге nginx). Проверять DNS лучше через
`https://dns.google/resolve?name=xn--80aad5dfbv.xn--p1ai&type=A`: локальный `nslookup` может
отвечать из кэша провайдера.

Сертификат один на три имени, хранится в `~/stathub/certbot/conf` (не в git, не трогается деплоем).

- **Первичный выпуск** — `bash ssl.sh issue` (CI вызывает сам после каждого деплоя; если сертификат уже
  настоящий, ничего не делает). До выпуска nginx работает на временном самоподписанном (`bash ssl.sh ensure`).
- **Продление** — контейнер `certbot` проверяет раз в 12 ч и продлевает за 30 дней до истечения;
  nginx перечитывает сертификат раз в 6 ч. Cron не нужен.
- **Добавить домен** — дописать его в `DOMAINS` в `ssl.sh` и в `nginx/conf.d/stathub.conf`, затем на сервере:

  ```bash
  docker compose run --rm --entrypoint certbot certbot certonly --webroot -w /var/www/certbot     --cert-name stathub --expand -d xn--80aad5dfbv.xn--p1ai -d www.xn--80aad5dfbv.xn--p1ai     -d admin.xn--80aad5dfbv.xn--p1ai -d <новый-домен>
  docker compose exec nginx nginx -s reload
  ```

- **Проверить срок** — `docker compose run --rm --entrypoint certbot certbot certificates`.

---

## Полезные команды

```bash
docker compose ps                 # состояние
docker compose logs -f --tail=100 api
docker stats --no-stream          # память/CPU контейнеров
docker compose restart api
docker system prune -f            # почистить старые образы (диск всего 20 ГБ)
```

## Ограничения текущей схемы

- Docker публикует порты 80 и 443 в обход ufw: открывать их в ufw не нужно, а закрыть через ufw нельзя.
- HSTS не включён: сначала убедиться, что HTTPS стабильно работает и продлевается.
