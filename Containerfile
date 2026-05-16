# Stage 1: Build React frontend
FROM node:22-alpine AS frontend
WORKDIR /src
RUN corepack enable && corepack prepare pnpm@11.0.0 --activate
COPY pnpm-workspace.yaml pnpm-lock.yaml ./
COPY app/package.json app/
RUN pnpm install --frozen-lockfile --ignore-scripts
COPY app/ app/
RUN pnpm --filter knowledge-search-ui build

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY knowledge-search.slnx Directory.Build.props ./
COPY src/ src/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# index.html y assets/ no van en dotnet publish — copiar directo del stage frontend
COPY --from=frontend /src/src/Api/index.html .
COPY --from=frontend /src/src/Api/assets/ assets/

EXPOSE 5111
ENV ASPNETCORE_URLS=http://+:5111
ENV FILE_WATCHER=polling
ENV KNOWLEDGE_DB=/data/db/knowledge.db
ENV KNOWLEDGE_DIRS=/data/knowledge
ENV SKILLS_DIR=/data/skills

HEALTHCHECK --interval=30s --timeout=5s --retries=3 \
  CMD wget -qO- http://localhost:5111/health || exit 1

ENTRYPOINT ["dotnet", "Api.dll"]
