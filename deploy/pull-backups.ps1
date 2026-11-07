# Забирает новые бэкапы с VPS на этот компьютер (запускается Планировщиком заданий Windows).
# Качает только файлы, которых ещё нет локально; локальные копии старше KeepDays удаляет.
# Лог: <Dest>\pull.log
param(
    [string]$Server   = 'shadmin@91.206.92.28',
    [string]$Key      = "$HOME\.ssh\stathub_backup",
    [string]$Dest     = "$HOME\Backups\stathub",
    [int]   $KeepDays = 60
)

$ErrorActionPreference = 'Stop'
$ssh = "$env:WINDIR\System32\OpenSSH\ssh.exe"
$scp = "$env:WINDIR\System32\OpenSSH\scp.exe"
$opts = @('-i', $Key, '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=30')

New-Item -ItemType Directory -Force $Dest | Out-Null
$log = Join-Path $Dest 'pull.log'
function Write-Log([string]$msg) {
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $msg" | Add-Content -Encoding UTF8 $log
}

try {
    $list = & $ssh @opts $Server "find ~/stathub/backups -maxdepth 1 -type f \( -name 'db_*.dump' -o -name 'uploads_*.tar.gz' \) -printf '%f\n'"
    if ($LASTEXITCODE -ne 0) { throw "ssh: код выхода $LASTEXITCODE" }

    $new = @($list | Where-Object { $_ -and -not (Test-Path (Join-Path $Dest $_)) })
    foreach ($f in $new) {
        $part = Join-Path $Dest "$f.part"
        & $scp @opts "${Server}:stathub/backups/$f" $part
        if ($LASTEXITCODE -ne 0) { throw "scp ${f}: код выхода $LASTEXITCODE" }
        Move-Item -Force $part (Join-Path $Dest $f)
        Write-Log "скачан $f"
    }
    if ($new.Count -eq 0) { Write-Log 'новых бэкапов нет' }

    Get-ChildItem $Dest -File | Where-Object {
        ($_.Name -like 'db_*.dump' -or $_.Name -like 'uploads_*.tar.gz') -and
        $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays)
    } | Remove-Item -Force
}
catch {
    Write-Log "ОШИБКА: $_"
    exit 1
}
