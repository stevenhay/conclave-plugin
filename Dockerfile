FROM mcr.microsoft.com/dotnet/sdk:9.0 as BUILD

COPY MareAPI /server/MareAPI
COPY MareSynchronos /server/MareSynchronos

WORKDIR /server/MareSynchronos

RUN apt-get update && apt-get install -y wget unzip

RUN mkdir -p /root/.xlcore/dalamud/Hooks/dev/ && \
    wget -O latest.zip https://goatcorp.github.io/dalamud-distrib/stg/latest.zip && \
    unzip -q latest.zip -d /root/.xlcore/dalamud/Hooks/dev/ && \
    rm latest.zip

RUN dotnet publish \
        --configuration=Release \
	--framework=net9.0-windows7.0 \
	--runtime win-x64 \
        --output=/build \
        MareSynchronos.csproj

FROM scratch AS export
COPY --from=BUILD /build /
