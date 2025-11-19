FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
 
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["/Genelife.Domain/Genelife.Domain.csproj", "Genelife.Domain/Genelife.Domain.csproj"]
RUN dotnet restore "./Genelife.Domain/Genelife.Domain.csproj"
COPY ./Genelife.Domain ./Genelife.Domain
WORKDIR "/src/Genelife.Domain"
RUN dotnet build "./Genelife.Domain.csproj" -c $BUILD_CONFIGURATION -o /app/build
 
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