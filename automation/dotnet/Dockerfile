FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore SundownMedia.ContentOps.sln
RUN dotnet build SundownMedia.ContentOps.sln --configuration Release --no-restore
RUN dotnet publish src/SundownMedia.ContentOps.Cli/SundownMedia.ContentOps.Cli.csproj --configuration Release --runtime linux-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false --output /app/publish

FROM ubuntu:24.04 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates ffmpeg sqlite3 && rm -rf /var/lib/apt/lists/*
RUN groupadd --gid 1001 contentops && useradd --uid 1001 --gid contentops --shell /bin/bash --create-home contentops
WORKDIR /app
COPY --from=build /app/publish/SundownMedia.ContentOps.Cli ./contentops
RUN chmod +x /app/contentops && chown -R contentops:contentops /app /home/contentops
USER contentops
ENTRYPOINT ["/app/contentops"]
CMD ["--help"]
