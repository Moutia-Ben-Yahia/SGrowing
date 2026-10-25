# Stage 1: Build .NET 10 Web API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["SGrowing.slnx", "./"]
COPY ["src/backend/SGrowing.Core/SGrowing.Core.csproj", "src/backend/SGrowing.Core/"]
COPY ["src/backend/SGrowing.Api/SGrowing.Api.csproj", "src/backend/SGrowing.Api/"]
COPY ["tests/SGrowing.Tests/SGrowing.Tests.csproj", "tests/SGrowing.Tests/"]

RUN dotnet restore "src/backend/SGrowing.Api/SGrowing.Api.csproj"

# Copy source code and publish
COPY . .
WORKDIR "/src/src/backend/SGrowing.Api"
RUN dotnet publish "SGrowing.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "SGrowing.Api.dll"]
