# Runtime-образ API для деплоя из CI. Собирается НА СЕРВЕРЕ из готового `dotnet publish`:
# CI синхронизирует publish/ через rsync (передаются только изменившиеся файлы), затем
#   cd ~/stathub/api && docker build -t stathub-api:<sha> .
# Базовый aspnet:10.0 берётся из локального кэша, поэтому по сети не гоняется.
# Для локальной сборки «с нуля» используйте корневой Dockerfile.

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY publish/ .

# Логотипы пишутся в wwwroot/uploads (LocalLogoStorage). Каталог создаётся заранее и отдаётся
# непривилегированному пользователю образа: на него монтируется volume, и Docker при первом
# создании volume перенесёт этого владельца.
RUN mkdir -p /app/wwwroot/uploads/logos \
    && chown -R "$APP_UID:$APP_UID" /app/wwwroot

USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Stathub.WebApi.dll"]
