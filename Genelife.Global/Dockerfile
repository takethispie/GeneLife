FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Genelife.Global.csproj", "Genelife.Global/"]
RUN dotnet restore "Genelife.Global/Genelife.Global.csproj"

COPY . Genelife.Global/.
RUN dotnet publish -c Release -o /app Genelife.Global/Genelife.Global.csproj 

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS publish
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Genelife.Global.dll"]