# Stage 1: Build .NET 10 Web API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["MarketingStudio.slnx", "./"]
COPY ["src/backend/MarketingStudio.Core/MarketingStudio.Core.csproj", "src/backend/MarketingStudio.Core/"]
COPY ["src/backend/MarketingStudio.Api/MarketingStudio.Api.csproj", "src/backend/MarketingStudio.Api/"]
COPY ["tests/MarketingStudio.Tests/MarketingStudio.Tests.csproj", "tests/MarketingStudio.Tests/"]

RUN dotnet restore "src/backend/MarketingStudio.Api/MarketingStudio.Api.csproj"

# Copy source code and publish
COPY . .
WORKDIR "/src/src/backend/MarketingStudio.Api"
RUN dotnet publish "MarketingStudio.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: ASP.NET Core Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "MarketingStudio.Api.dll"]
