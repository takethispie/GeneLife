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
 
FROM build as depbuild
WORKDIR /src
COPY ["Genelife.Life/Genelife.Life.csproj", "Genelife.Life/"]
RUN dotnet restore "./Genelife.Life/Genelife.Life.csproj"
COPY ./Genelife.Life ./Genelife.Life
WORKDIR "/src/Genelife.Life"
RUN dotnet build "./Genelife.Life.csproj" -c $BUILD_CONFIGURATION -o /app/build
 
FROM depbuild AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Genelife.Life.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
 
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Genelife.Life.dll"]