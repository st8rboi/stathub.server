#!/usr/bin/env bash
# Ежедневный бэкап: дамп Postgres + архив логотипов. Хранит KEEP_DAYS дней.
# Cron (crontab -e):
#   30 3 * * * /home/shadmin/stathub/backup.sh >> /home/shadmin/stathub/backups/backup.log 2>&1
# ВАЖНО: бэкапы на том же сервере не спасут при его потере — периодически забирайте их к себе.
set -euo pipefail

cd "$(dirname "$0")"

BACKUP_DIR="${BACKUP_DIR:-$PWD/backups}"
KEEP_DAYS="${KEEP_DAYS:-14}"
STAMP="$(date +%F_%H-%M)"

mkdir -p "$BACKUP_DIR"

# Пишем во временный файл и переименовываем, чтобы не оставить «половинный» дамп при сбое.
docker compose exec -T postgres pg_dump -U stathub -d stathub --format=custom \
    > "$BACKUP_DIR/db_$STAMP.dump.tmp"
mv "$BACKUP_DIR/db_$STAMP.dump.tmp" "$BACKUP_DIR/db_$STAMP.dump"

docker run --rm \
    -v stathub_uploads:/data:ro \
    -v "$BACKUP_DIR":/backup \
    alpine:3.20 \
    tar czf "/backup/uploads_$STAMP.tar.gz" -C /data .

find "$BACKUP_DIR" -maxdepth 1 -type f \( -name 'db_*.dump' -o -name 'uploads_*.tar.gz' \) \
    -mtime +"$KEEP_DAYS" -delete

echo "$(date '+%F %T') backup ok: db_$STAMP.dump, uploads_$STAMP.tar.gz"
