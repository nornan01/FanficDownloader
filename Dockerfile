ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

COPY FanficDownloader.Web/FanficDownloader.Web.csproj FanficDownloader.Web/
COPY FanficDownloader.Application/FanficDownloader.Application.csproj FanficDownloader.Application/
COPY FanficDownloader.Core/FanficDownloader.Core.csproj FanficDownloader.Core/
COPY FanficDownloader.Bot/FanficDownloader.bot.csproj FanficDownloader.Bot/
COPY FanficDownloader.slnx ./

RUN dotnet restore FanficDownloader.Web/FanficDownloader.Web.csproj

COPY . .

RUN dotnet publish FanficDownloader.Web/FanficDownloader.Web.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "FanficDownloader.Web.dll"]
