FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["GenelifeMain.csproj", "GenelifeMain/"]
RUN dotnet restore "GenelifeMain/GenelifeMain.csproj"

COPY . GenelifeMain/.
RUN dotnet publish -c Release -o /app GenelifeMain/GenelifeMain.csproj 

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS publish
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "GenelifeMain.dll"]