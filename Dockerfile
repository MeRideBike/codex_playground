FROM node:22-alpine AS web-build
WORKDIR /src/ForgeBoard.Web
COPY src/ForgeBoard.Web/package.json src/ForgeBoard.Web/package-lock.json ./
RUN npm ci
COPY src/ForgeBoard.Web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ForgeBoard.sln
RUN dotnet publish src/ForgeBoard.Api/ForgeBoard.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends git \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY --from=web-build /src/ForgeBoard.Web/dist ./wwwroot
COPY samples ./samples
ENV ASPNETCORE_URLS=http://+:8080
ENV ForgeBoard__SamplesRoot=/app/samples
ENV ForgeBoard__RunWorkspacesRoot=/app/data/runs
EXPOSE 8080
ENTRYPOINT ["dotnet", "ForgeBoard.Api.dll"]
