FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY SGrowing.slnx ./
COPY src/backend/SGrowing.Core/ src/backend/SGrowing.Core/
COPY src/backend/SGrowing.Api/ src/backend/SGrowing.Api/
COPY src/backend/SGrowing.Worker/ src/backend/SGrowing.Worker/

WORKDIR /app/src/backend/SGrowing.Worker
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "SGrowing.Worker.dll"]
