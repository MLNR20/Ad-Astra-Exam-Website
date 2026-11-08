FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY AAExamManagementSystem/AAExamManagementSystem.csproj AAExamManagementSystem/
COPY AAExamManagementSystem.Models/AAExamManagementSystem.Models.csproj AAExamManagementSystem.Models/
COPY AAExamManagementSystem.Repository/AAExamManagementSystem.Repository.csproj AAExamManagementSystem.Repository/
COPY AAExamManagementSystem.Migrations.Postgres/AAExamManagementSystem.Migrations.Postgres.csproj AAExamManagementSystem.Migrations.Postgres/
COPY AAExamManagementSystem.Migrations.SqlServer/AAExamManagementSystem.Migrations.SqlServer.csproj AAExamManagementSystem.Migrations.SqlServer/
RUN dotnet restore AAExamManagementSystem/AAExamManagementSystem.csproj

COPY AAExamManagementSystem/ AAExamManagementSystem/
COPY AAExamManagementSystem.Models/ AAExamManagementSystem.Models/
COPY AAExamManagementSystem.Repository/ AAExamManagementSystem.Repository/
COPY AAExamManagementSystem.Migrations.Postgres/ AAExamManagementSystem.Migrations.Postgres/
COPY AAExamManagementSystem.Migrations.SqlServer/ AAExamManagementSystem.Migrations.SqlServer/

RUN dotnet publish AAExamManagementSystem/AAExamManagementSystem.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AAExamManagementSystem.dll"]
