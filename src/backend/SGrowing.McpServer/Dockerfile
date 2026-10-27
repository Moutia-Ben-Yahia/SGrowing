# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/backend/SGrowing.Core/SGrowing.Core.csproj", "src/backend/SGrowing.Core/"]
COPY ["src/backend/SGrowing.ServiceDefaults/SGrowing.ServiceDefaults.csproj", "src/backend/SGrowing.ServiceDefaults/"]
COPY ["src/backend/SGrowing.McpServer/SGrowing.McpServer.csproj", "src/backend/SGrowing.McpServer/"]
RUN dotnet restore "src/backend/SGrowing.McpServer/SGrowing.McpServer.csproj"

# Copy source code and build
COPY ["src/backend/SGrowing.Core/", "src/backend/SGrowing.Core/"]
COPY ["src/backend/SGrowing.ServiceDefaults/", "src/backend/SGrowing.ServiceDefaults/"]
COPY ["src/backend/SGrowing.McpServer/", "src/backend/SGrowing.McpServer/"]
WORKDIR "/src/src/backend/SGrowing.McpServer"
RUN dotnet build "SGrowing.McpServer.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "SGrowing.McpServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 7890
ENV ASPNETCORE_URLS=http://+:7890
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SGrowing.McpServer.dll"]
