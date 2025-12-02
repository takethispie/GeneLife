FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
 
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["/Genelife.Life.Messages/Genelife.Life.Messages.csproj", "Genelife.Life.Messages/Genelife.Life.Messages.csproj"]
RUN dotnet restore "./Genelife.Life.Messages/Genelife.Life.Messages.csproj"
COPY ./Genelife.Life.Messages ./Genelife.Life.Messages
WORKDIR "/src/Genelife.Life.Messages"
RUN dotnet build "./Genelife.Life.Messages.csproj" -c $BUILD_CONFIGURATION -o /app/build

WORKDIR /src
COPY ["/Genelife.Work.Messages/Genelife.Work.Messages.csproj", "Genelife.Work.Messages/Genelife.Work.Messages.csproj"]
RUN dotnet restore "./Genelife.Work.Messages/Genelife.Work.Messages.csproj"
COPY ./Genelife.Work.Messages ./Genelife.Work.Messages
WORKDIR "/src/Genelife.Work.Messages"
RUN dotnet build "./Genelife.Work.Messages.csproj" -c $BUILD_CONFIGURATION -o /app/build

WORKDIR /src
COPY ["/Genelife.Global.Messages/Genelife.Global.Messages.csproj", "Genelife.Global.Messages/Genelife.Global.Messages.csproj"]
RUN dotnet restore "./Genelife.Global.Messages/Genelife.Global.Messages.csproj"
COPY ./Genelife.Global.Messages ./Genelife.Global.Messages
WORKDIR "/src/Genelife.Global.Messages"
RUN dotnet build "./Genelife.Global.Messages.csproj" -c $BUILD_CONFIGURATION -o /app/build

WORKDIR /src
COPY ["/Genelife.Life/Genelife.Life.csproj", "Genelife.Life/Genelife.Life.csproj"]
RUN dotnet restore "./Genelife.Life/Genelife.Life.csproj"
COPY ./Genelife.Life ./Genelife.Life
WORKDIR "/src/Genelife.Life"
RUN dotnet build "./Genelife.Life.csproj" -c $BUILD_CONFIGURATION -o /app/build

WORKDIR /src
COPY ["/Genelife.Work/Genelife.Work.csproj", "Genelife.Work/Genelife.Work.csproj"]
RUN dotnet restore "./Genelife.Work/Genelife.Work.csproj"
COPY ./Genelife.Work ./Genelife.Work
WORKDIR "/src/Genelife.Work"
RUN dotnet build "./Genelife.Work.csproj" -c $BUILD_CONFIGURATION -o /app/build
 
FROM build AS depbuild
WORKDIR /src
COPY ["Genelife.API/Genelife.API.csproj", "Genelife.API/"]
RUN dotnet restore "./Genelife.API/Genelife.API.csproj"
COPY ./Genelife.API ./Genelife.API
WORKDIR "/src/Genelife.API"
RUN dotnet build "./Genelife.API.csproj" -c $BUILD_CONFIGURATION -o /app/build
 
FROM depbuild AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Genelife.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
 
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Genelife.API.dll"]