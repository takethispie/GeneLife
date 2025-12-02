FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Genelife.Work/Genelife.Work.csproj", "Genelife.Work/"]
RUN dotnet restore "Genelife.Work/Genelife.Work.csproj"
COPY . .
WORKDIR "/src/Genelife.Work"
RUN dotnet build "./Genelife.Work.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS depbuild
WORKDIR /src
COPY ["Genelife.Work.Messages/Genelife.Work.Messages.csproj", "Genelife.Work.Messages/"]
RUN dotnet restore "./Genelife.Work.Messages/Genelife.Work.Messages.csproj"
COPY ./Genelife.Work.Messages ./Genelife.Work.Messages
WORKDIR "/src/Genelife.Work.Messages"
RUN dotnet build "./Genelife.Work.Messages.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM depbuild AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Genelife.Work.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Genelife.Work.dll"]
