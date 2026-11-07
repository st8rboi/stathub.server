# Образ API (Stathub.WebApi). Сборка из корня stathub.server:
#   docker build --platform linux/amd64 -t stathub-api:latest .
# Деплой и запуск — см. deploy/README.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# global.json сознательно не копируется: он фиксирует локальный SDK 10.0.4xx, а образ sdk:10.0
# поставляется с другой feature-полосой — сборка упала бы с "compatible SDK not found".
COPY Directory.Build.props Directory.Packages.props NuGet.Config ./
COPY src/ src/

RUN dotnet publish src/Stathub.WebApi/Stathub.WebApi.csproj \
    -c Release \
    -o /app/publish \
    -p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Логотипы пишутся в wwwroot/uploads (LocalLogoStorage). Каталог создаётся заранее и отдаётся
# непривилегированному пользователю образа: на него монтируется volume, и Docker при первом
# создании volume перенесёт этого владельца.
RUN mkdir -p /app/wwwroot/uploads/logos \
    && chown -R "$APP_UID:$APP_UID" /app/wwwroot

USER $APP_UID

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Stathub.WebApi.dll"]
