# 開発環境

## Dev Container での開発

このリポジトリは VS Code Dev Containers / GitHub Codespaces で .NET 8 開発環境を起動できるように `.devcontainer/` を用意している。

### 前提条件

- Docker Desktop または Docker Engine
- VS Code と Dev Containers 拡張機能（ローカルで利用する場合）

### 起動手順

1. VS Code でこのリポジトリを開く。
2. コマンドパレットから **Dev Containers: Reopen in Container** を実行する。
3. コンテナ作成後、`postCreateCommand` により `dotnet restore MailLinkage.sln` が自動実行される。

コンテナ内には .NET 8 SDK、Docker CLI、SQLite CLI が含まれる。Dev Container は `MailBatchSample/docker-compose.yml` と `.devcontainer/docker-compose.yml` を組み合わせた Docker Compose サービスとして起動し、検証用サービスと同じ Compose ネットワークに参加する。

Docker はホスト側 Docker を利用する設定のため、コンテナ内から `docker compose` を使って `MailBatchSample/docker-compose.yml` の検証環境を操作できる。

## 接続先の考え方

Dev Container 内から Docker Compose のサービスへ接続する場合は、`mailreceiver-api:8080` や `mailserver:3025` / `mailserver:3143` のサービス名を利用する。Dev Container を抜けてホスト OS のターミナルで確認する場合だけ、Compose が公開した `localhost` のポートを参照する。

| 用途 | Dev Container / Compose 内 | ホスト OS |
| --- | --- | --- |
| SMTP | `mailserver:3025` | `localhost:1025` |
| IMAP | `mailserver:3143` | `localhost:1143` |
| MailReceiver.Api | `http://mailreceiver-api:8080` | `http://localhost:5000` |

## よく使うコマンド

```bash
dotnet restore MailLinkage.sln
dotnet build MailLinkage.sln
dotnet run --project MailBatchSample/src/MailReceiver.Api/MailReceiver.Api.csproj
docker compose -f MailBatchSample/docker-compose.yml config
docker compose -f MailBatchSample/docker-compose.yml up -d --build mailserver mailreceiver-api
docker compose -f MailBatchSample/docker-compose.yml stop mailserver mailreceiver-api
docker compose -f MailBatchSample/docker-compose.yml rm -f mailserver mailreceiver-api
```

## ホスト OS のターミナルで実行する場合

アプリの既定値は Dev Container から Compose サービス名へ接続する構成である。ホスト OS で `dotnet run` する場合は次のように接続先を上書きする。

```bash
# ホスト OS で TestMailSender を実行する例
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Smtp:Host=localhost Smtp:Port=1025 Mail:Mode=target

# ホスト OS で MailBatch.Console を実行する例
dotnet run --project MailBatchSample/src/MailBatch.Console/MailBatch.Console.csproj -- Imap:Host=localhost Imap:Port=1143 Api:BaseUrl=http://localhost:5000
```
