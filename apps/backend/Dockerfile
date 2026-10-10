# Этап 1: Сборка
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Копируем csproj и восстанавливаем зависимости отдельным слоем
COPY *.csproj .
RUN dotnet restore

# Копируем всё остальное и собираем
COPY . .
RUN dotnet publish -c Release -o /app

# Этап 2: Запуск
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Копируем скомпилированное приложение из этапа сборки
COPY --from=build /app .

# Указываем порт, который слушает приложение (по умолчанию ASP.NET Core в контейнере слушает 8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "backend.dll"]
