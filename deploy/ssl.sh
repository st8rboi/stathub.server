#!/usr/bin/env bash
# HTTPS-сертификаты Let's Encrypt для nginx. Запускается из ~/stathub; CI вызывает его сам при деплое.
#
#   bash ssl.sh ensure  — для каждого сертификата, которого ещё нет, положить временный самоподписанный,
#                         иначе nginx с ssl-конфигом не стартует. Существующие не трогает.
#   bash ssl.sh issue   — заменить временные сертификаты настоящими. Нужен запущенный nginx (отвечает на
#                         проверку по http://.../.well-known/acme-challenge/). Уже настоящие не трогает.
#                         Сертификаты выпускаются независимо: неудача одного не мешает другому.
#
# Сертификаты:
#   stathub — статхаб.рф, www, admin (обычный, 90 дней; DNS доменов должен указывать на этот сервер)
#   ip      — 91.206.92.250 (профиль shortlived, ~6 дней; нужен certbot >= 5.4). ОТКЛЮЧЁН: HTTPS по IP
#             на 443 делает сервер похожим на VPN/прокси, и ТСПУ начинает резать TLS. Код оставлен —
#             чтобы включить, раскомментировать строку [ip] в CERTS и вернуть server-блок в nginx.
#
# Продление — сервис certbot в docker-compose.yml (раз в 12 ч), nginx перечитывает сертификаты раз в 6 ч.
# Файлы — в ./certbot/conf (в git не хранятся). Certbot пишет их от root с правами 700 на live/ и archive/,
# поэтому все проверки и правки там делаются через одноразовый контейнер от root, а не от пользователя.
set -euo pipefail

cd "$(dirname "$0")"

SERVER_IP=91.206.92.250

# Имя сертификата → аргументы certbot.
declare -A CERTS=(
    [stathub]="-d xn--80aad5dfbv.xn--p1ai -d www.xn--80aad5dfbv.xn--p1ai -d admin.xn--80aad5dfbv.xn--p1ai"
    # [ip]="--ip-address $SERVER_IP --preferred-profile shortlived"
)

# Когда продлевать (renew_before_expiry в renewal/<имя>.conf). По умолчанию certbot продлевает за 30 дней —
# для 6-дневного сертификата это означало бы перевыпуск на каждом запуске (раз в 12 ч) и упор в лимиты
# Let's Encrypt. 3 дня → перевыпуск примерно раз в 3 дня, с запасом на сбои.
declare -A RENEW_BEFORE=(
    [ip]="3 days"
)

# Выполнить команду от root с доступом к certbot/conf (смонтирован в /c).
# Образ alpine:3.20 уже есть на сервере — его использует backup.sh.
as_root() {
    docker run --rm -v "$PWD/certbot/conf:/c" "$@"
}

ensure_one() {
    local name=$1
    # Сертификат на IP выпущен для другого адреса (сервер сменил IP) — сбрасываем на временный,
    # issue выпустит новый. Для какого IP выпущен — записано в /c/.issued-ip.
    if [ "$name" = ip ] \
        && ! as_root alpine:3.20 test -f "/c/.dummy-ip" \
        && ! as_root alpine:3.20 grep -qxF "$SERVER_IP" /c/.issued-ip; then
        echo "[ip] сертификат выпущен не для $SERVER_IP — перевыпускаю"
        as_root alpine:3.20 rm -rf /c/live/ip /c/archive/ip /c/renewal/ip.conf
    fi
    if as_root alpine:3.20 test -f "/c/live/$name/fullchain.pem"; then
        return 0
    fi
    echo "[$name] сертификата нет — создаю временный самоподписанный"
    local tmp
    tmp="$(mktemp -d)"
    openssl req -x509 -nodes -newkey rsa:2048 -days 7 -subj "/CN=localhost" \
        -keyout "$tmp/privkey.pem" -out "$tmp/fullchain.pem" 2>/dev/null
    as_root -v "$tmp:/t:ro" alpine:3.20 sh -c \
        "mkdir -p /c/live/$name && cp /t/privkey.pem /t/fullchain.pem /c/live/$name/ && touch /c/.dummy-$name"
    rm -rf "$tmp"
}

issue_one() {
    local name=$1
    if ! as_root alpine:3.20 test -f "/c/.dummy-$name"; then
        echo "[$name] сертификат Let's Encrypt уже выпущен"
        return 0
    fi

    # certbot не выпускает сертификат поверх чужих файлов в live/ — убираем временный.
    # nginx продолжает работать: старый сертификат уже загружен в память.
    as_root alpine:3.20 rm -rf "/c/live/$name" "/c/archive/$name" "/c/renewal/$name.conf"

    # shellcheck disable=SC2086  # аргументы из CERTS намеренно разбиваются по пробелам
    if docker compose run --rm --no-deps --entrypoint certbot certbot \
        certonly --webroot -w /var/www/certbot --cert-name "$name" ${CERTS[$name]} \
        --agree-tos --register-unsafely-without-email --non-interactive; then
        as_root alpine:3.20 rm -f "/c/.dummy-$name"
        if [ -n "${RENEW_BEFORE[$name]:-}" ]; then
            as_root alpine:3.20 sed -i "1i renew_before_expiry = ${RENEW_BEFORE[$name]}" "/c/renewal/$name.conf"
        fi
        if [ "$name" = ip ]; then
            as_root alpine:3.20 sh -c "echo '$SERVER_IP' > /c/.issued-ip"
        fi
        echo "[$name] сертификат Let's Encrypt выпущен"
    else
        echo "[$name] не удалось выпустить сертификат (${CERTS[$name]})" >&2
        ensure_one "$name"   # вернуть временный, чтобы nginx мог перезапуститься
        return 1
    fi
}

ensure() {
    mkdir -p certbot/conf certbot/www
    for name in "${!CERTS[@]}"; do ensure_one "$name"; done
}

issue() {
    local failed=0
    for name in "${!CERTS[@]}"; do issue_one "$name" || failed=1; done
    docker compose exec -T nginx nginx -s reload
    return "$failed"
}

case "${1:-}" in
    ensure) ensure ;;
    issue)  issue ;;
    *) echo "Использование: bash ssl.sh ensure|issue" >&2; exit 2 ;;
esac
