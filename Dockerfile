# Stage 1: Build Angular frontend
FROM node:20-alpine AS frontend-build
WORKDIR /app/client
COPY client/package*.json ./
RUN npm ci
COPY client/ ./
RUN npm run build -- --configuration=production

# Stage 2: Build .NET backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /app
COPY server/*.csproj ./server/
RUN dotnet restore server/Meuhte.Server.csproj
COPY server/ ./server/
RUN dotnet publish server/Meuhte.Server.csproj -c Release -o /app/publish

# Stage 3: Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy published .NET app
COPY --from=backend-build /app/publish ./

# Copy Angular build output to wwwroot
COPY --from=frontend-build /app/client/dist/meuhte-client ./wwwroot/

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Meuhte.Server.dll"]
