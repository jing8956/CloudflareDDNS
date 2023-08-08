FROM mcr.microsoft.com/dotnet/sdk:7.0 AS publish

# Install NativeAOT build prerequisites
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       clang zlib1g-dev

WORKDIR /src
COPY . .
RUN dotnet publish "CloudflareDDNS.fsproj" -c Release -p PublishAot=true -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:7.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [ "/app/CloudflareDDNS" ]
