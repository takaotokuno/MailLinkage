# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MailLinkage.sln ./
COPY MailBatchSample/src/MailReceiver.Api/MailReceiver.Api.csproj MailBatchSample/src/MailReceiver.Api/
RUN dotnet restore MailBatchSample/src/MailReceiver.Api/MailReceiver.Api.csproj

COPY MailBatchSample/src/MailReceiver.Api/ MailBatchSample/src/MailReceiver.Api/
RUN dotnet publish MailBatchSample/src/MailReceiver.Api/MailReceiver.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MailReceiver.Api.dll"]
