#!/usr/bin/env bash
# HTTPS-сертификат Let's Encrypt для nginx. Запускается из ~/stathub; CI вызывает его сам при деплое.
#
#   bash ssl.sh ensure  — если сертификата ещё нет, положить временный самоподписанный,
#                         иначе nginx с ssl-конфигом не стартует. Ничего не делает, если сертификат есть.
#   bash ssl.sh issue   — заменить временный сертификат настоящим. Нужны запущенный nginx (отвечает на
#                         проверку по http://.../.well-known/acme-challenge/) и DNS всех доменов на этот сервер.
#                         Ничего не делает, если сертификат уже настоящий.
#
# Продление — сервис certbot в docker-compose.yml (раз в 12 ч), nginx перечитывает сертификат раз в 6 ч.
# Файлы — в ./certbot/conf (в git не хранятся). Certbot пишет их от root с правами 700 на live/ и archive/,
# поэтому все проверки и правки там делаются через одноразовый контейнер от root, а не от пользователя.
set -euo pipefail

cd "$(dirname "$0")"

NAME=stathub
DOMAINS=(
    xn--80aad5dfbv.xn--p1ai        # статхаб.рф
    www.xn--80aad5dfbv.xn--p1ai    # www.статхаб.рф
    admin.xn--80aad5dfbv.xn--p1ai  # admin.статхаб.рф
)

# Внутри вспомогательного контейнера certbot/conf смонтирован в /c.
LIVE="/c/live/$NAME"
DUMMY_MARK="/c/.dummy-$NAME"

# Выполнить команду sh от root с доступом к certbot/conf (образ alpine:3.20 уже есть — его использует backup.sh).
as_root() {
    docker run --rm -v "$PWD/certbot/conf:/c" "$@"
}

ensure() {
    mkdir -p certbot/conf certbot/www
    if as_root alpine:3.20 test -f "$LIVE/fullchain.pem"; then
        return 0
    fi
    echo "Сертификата нет — создаю временный самоподписанный"
    local tmp
    tmp="$(mktemp -d)"
    openssl req -x509 -nodes -newkey rsa:2048 -days 7 -subj "/CN=localhost" \
        -keyout "$tmp/privkey.pem" -out "$tmp/fullchain.pem" 2>/dev/null
    as_root -v "$tmp:/t:ro" alpine:3.20 \
        sh -c "mkdir -p '$LIVE' && cp /t/privkey.pem /t/fullchain.pem '$LIVE/' && touch '$DUMMY_MARK'"
    rm -rf "$tmp"
}

issue() {
    if ! as_root alpine:3.20 test -f "$DUMMY_MARK"; then
        echo "Сертификат Let's Encrypt уже выпущен"
        return 0
    fi

    local args=()
    for d in "${DOMAINS[@]}"; do args+=(-d "$d"); done

    # certbot не выпускает сертификат поверх чужих файлов в live/ — убираем временный.
    # nginx продолжает работать: старый сертификат уже загружен в память.
    as_root alpine:3.20 rm -rf "$LIVE" "/c/archive/$NAME" "/c/renewal/$NAME.conf"

    if docker compose run --rm --no-deps --entrypoint certbot certbot \
        certonly --webroot -w /var/www/certbot \
        --cert-name "$NAME" "${args[@]}" \
        --agree-tos --register-unsafely-without-email --non-interactive; then
        as_root alpine:3.20 rm -f "$DUMMY_MARK"
        docker compose exec -T nginx nginx -s reload
        echo "Сертификат Let's Encrypt выпущен"
    else
        echo "Не удалось выпустить сертификат. Проверьте, что все домены указывают на этот сервер:" >&2
        printf '  %s\n' "${DOMAINS[@]}" >&2
        ensure   # вернуть временный, чтобы nginx мог перезапуститься
        return 1
    fi
}

case "${1:-}" in
    ensure) ensure ;;
    issue)  issue ;;
    *) echo "Использование: bash ssl.sh ensure|issue" >&2; exit 2 ;;
esac
