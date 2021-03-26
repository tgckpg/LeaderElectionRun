#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:5.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["LeaderElectionRun/LeaderElectionRun.csproj", "LeaderElectionRun/"]
RUN dotnet restore "LeaderElectionRun/LeaderElectionRun.csproj"
COPY . .
WORKDIR "/src/LeaderElectionRun"
RUN dotnet build "LeaderElectionRun.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LeaderElectionRun.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LeaderElectionRun.dll"]