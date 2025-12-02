FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["/Genelife.Global.Messages/Genelife.Global.Messages.csproj", "Genelife.Global.Messages/Genelife.Global.Messages.csproj"]
RUN dotnet restore "./Genelife.Global.Messages/Genelife.Global.Messages.csproj"
COPY ./Genelife.Global.Messages ./Genelife.Global.Messages
WORKDIR "/src/Genelife.Global.Messages"
RUN dotnet build "./Genelife.Global.Messages.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build as depbuild
WORKDIR /src
COPY ["Genelife.Global.csproj", "Genelife.Global/"]
RUN dotnet restore "Genelife.Global/Genelife.Global.csproj"
COPY . Genelife.Global/.
RUN dotnet publish -c Release -o /app Genelife.Global/Genelife.Global.csproj 

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS publish
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "Genelife.Global.dll"]