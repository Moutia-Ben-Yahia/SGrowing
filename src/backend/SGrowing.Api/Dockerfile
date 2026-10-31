# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/backend/SGrowing.Core/SGrowing.Core.csproj", "src/backend/SGrowing.Core/"]
COPY ["src/backend/SGrowing.ServiceDefaults/SGrowing.ServiceDefaults.csproj", "src/backend/SGrowing.ServiceDefaults/"]
COPY ["src/backend/SGrowing.Api/SGrowing.Api.csproj", "src/backend/SGrowing.Api/"]
RUN dotnet restore "src/backend/SGrowing.Api/SGrowing.Api.csproj"

# Copy source code and build
COPY ["src/backend/SGrowing.Core/", "src/backend/SGrowing.Core/"]
COPY ["src/backend/SGrowing.ServiceDefaults/", "src/backend/SGrowing.ServiceDefaults/"]
COPY ["src/backend/SGrowing.Api/", "src/backend/SGrowing.Api/"]
WORKDIR "/src/src/backend/SGrowing.Api"
RUN dotnet build "SGrowing.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SGrowing.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGrowing.Api.dll"]
