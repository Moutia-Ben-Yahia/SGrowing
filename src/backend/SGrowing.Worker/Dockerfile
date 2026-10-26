# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/backend/SGrowing.Core/SGrowing.Core.csproj", "src/backend/SGrowing.Core/"]
COPY ["src/backend/SGrowing.Api/SGrowing.Api.csproj", "src/backend/SGrowing.Api/"]
COPY ["src/backend/SGrowing.Worker/SGrowing.Worker.csproj", "src/backend/SGrowing.Worker/"]
RUN dotnet restore "src/backend/SGrowing.Worker/SGrowing.Worker.csproj"

# Copy source code and build
COPY ["src/backend/SGrowing.Core/", "src/backend/SGrowing.Core/"]
COPY ["src/backend/SGrowing.Api/", "src/backend/SGrowing.Api/"]
COPY ["src/backend/SGrowing.Worker/", "src/backend/SGrowing.Worker/"]
WORKDIR "/src/src/backend/SGrowing.Worker"
RUN dotnet build "SGrowing.Worker.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SGrowing.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGrowing.Worker.dll"]
