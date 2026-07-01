# MailLinkage

MailLinkage は、Docker 上に構築したテスト用メールサーバに投入されたメールを .NET 8 のバッチアプリで IMAP 取得し、抽出したメール情報を連携用 API へ POST する検証用サンプルです。

初期スコープでは、処理結果を後から確認できる証跡を残すことを重視し、API は SQLite への保存と GET API による確認を提供します。認証認可、CRUD 画面、添付ファイル処理、複雑なリトライ制御は対象外です。

## Dev Container での開発

このリポジトリは VS Code Dev Containers / GitHub Codespaces で .NET 8 開発環境を起動できるように `.devcontainer/` を用意しています。

### 前提条件

- Docker Desktop または Docker Engine
- VS Code と Dev Containers 拡張機能（ローカルで利用する場合）

### 起動手順

1. VS Code でこのリポジトリを開きます。
2. コマンドパレットから **Dev Containers: Reopen in Container** を実行します。
3. コンテナ作成後、`postCreateCommand` により `dotnet restore MailLinkage.sln` が自動実行されます。

コンテナ内には .NET 8 SDK、Docker CLI、SQLite CLI が含まれます。Docker はホスト側 Docker を利用する設定のため、コンテナ内から `docker compose` を使って `MailBatchSample/docker-compose.yml` の検証環境を操作できます。

Dev Container 内から Docker Compose で公開したポートへ接続する場合、`localhost` は Dev Container 自身を指すため、Compose サービスへは `host.docker.internal` を使います。この README の動作確認コマンドは Dev Container 内で実行する前提で `host.docker.internal` を既定にしています。Dev Container を抜けてホスト OS のターミナルで実行する場合だけ、接続先を `localhost` に読み替えてください。

### よく使うコマンド

```bash
dotnet restore MailLinkage.sln
dotnet build MailLinkage.sln
dotnet run --project MailBatchSample/src/MailReceiver.Api/MailReceiver.Api.csproj
docker compose -f MailBatchSample/docker-compose.yml config
```

### ホスト OS のターミナルで実行する場合

Dev Container 外のホスト OS で `dotnet run` や `curl` を実行する場合は、Compose の公開ポートを直接 `localhost` で参照できます。アプリの既定値は Dev Container 向けのため、ホスト OS で `dotnet run` する場合は次のように接続先を上書きしてください。

```bash
# ホスト OS で TestMailSender を実行する例
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Smtp:Host=localhost Mail:Mode=target

# ホスト OS で MailBatch.Console を実行する例
dotnet run --project MailBatchSample/src/MailBatch.Console/MailBatch.Console.csproj -- Imap:Host=localhost Api:BaseUrl=http://localhost:5000
```

## ドキュメント

- [要件・スコープ](docs/01-requirements-and-scope.md)
- [構成設計](docs/02-architecture.md)
- [アプリケーション設計](docs/03-application-design.md)
- [データ・API 設計](docs/04-data-and-api-design.md)
- [実行・検証設計](docs/05-runtime-and-verification.md)

## 想定ディレクトリ構成

```text
MailBatchSample/
  docker-compose.yml

  src/
    MailBatch.Console/
      .NET 8 Console App

    MailReceiver.Api/
      ASP.NET Core Web API

    TestMailSender/
      .NET 8 Console App

  logs/
    batch-yyyyMMdd.log

  data/
    mailreceiver.db
```

## `logs/` と `data/` の配置方針

`logs/` と `data/` は、`MailBatchSample/` 直下に配置します。ソースコードや Docker Compose 定義から分離し、ローカル実行・Docker Compose 実行のどちらでも同じホスト側ディレクトリを参照できるようにします。

| パス | 用途 | 配置・運用方針 |
| --- | --- | --- |
| `MailBatchSample/logs/` | `MailBatch.Console` のバッチログ出力先 | バッチ実行時に日次ファイル `batch-yyyyMMdd.log` を出力します。ログは実行ごとの証跡として扱い、ソース管理対象には含めません。 |
| `MailBatchSample/data/` | `MailReceiver.Api` が利用する SQLite DB の配置先 | API 起動時または初期化時に `mailreceiver.db` を作成します。DB ファイルはローカル検証用の実行データとして扱い、ソース管理対象には含めません。 |

実行前にディレクトリが存在しない場合は、アプリケーションまたは起動手順で作成します。Docker Compose の `mailreceiver-api` は `MailBatchSample/data/` をコンテナ内の `/app/data` へバインドマウントします。これにより、Dev Container 内・ホスト OS・Compose コンテナのいずれから見ても、SQLite DB の実体はリポジトリ内の `MailBatchSample/data/mailreceiver.db` に揃います。


## ローカル検証の起動形態

このサンプルでは、ソリューションに含まれるアプリケーションを次の起動形態で使い分けます。

| コンポーネント | 起動方法 | 役割 |
| --- | --- | --- |
| `mailserver` | Docker Compose で常時起動 | SMTP / IMAP を提供する開発用メールサーバ |
| `MailReceiver.Api` | Docker Compose で常時起動 | バッチ結果を受け取り SQLite に保存する連携先 API |
| `TestMailSender` | `dotnet run` で必要時に起動 | 検証用メールをメールサーバへ投入する操作ツール |
| `MailBatch.Console` | `dotnet run` で必要時に起動 | メールボックスから対象メールを取得し API へ POST するバッチ |

`MailLinkage.sln` は開発・ビルド・テストの単位です。ソリューションに含まれるすべてのプロジェクトを Docker Compose の常駐サービスとして起動することは想定していません。

## Docker を起動する手順

メールサーバと連携先 API を Docker Compose で起動します。

```bash
cd MailBatchSample
docker compose up -d --build
```

起動状態は次のコマンドで確認できます。

```bash
docker compose ps
curl http://host.docker.internal:5000/health
```

Dev Container 内から利用する既定の接続先は次の通りです。

| 用途 | URL / 接続先 |
| --- | --- |
| SMTP | `host.docker.internal:1025` |
| IMAP | `host.docker.internal:1143` |
| MailReceiver.Api | `http://host.docker.internal:5000` |

Compose のホスト側公開ポートは `MAILSERVER_SMTP_HOST_PORT`、`MAILSERVER_IMAP_HOST_PORT`、`MAILRECEIVER_API_HOST_PORT` で上書きできます。GreenMail の初期ユーザー定義は `MAILSERVER_USERS` で上書きできます。既定値はローカル検証用の固定値であり、本番環境では利用しません。

## Docker が起動した状態でのテストメール送信方法

`TestMailSender` は `MailBatchSample/src/TestMailSender/appsettings.json` の SMTP 接続設定とメール既定値を読み込み、`TESTMAILSENDER_` プレフィックスの環境変数またはコマンドライン引数で上書きできます。

Docker Compose 起動後、リポジトリルートから次のように実行します。

```bash
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Mail:Mode=target
```

よく使う送信モードは次の通りです。

```bash
# バッチ対象条件に一致するメールを送信する
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Mail:Mode=target

# バッチ対象条件に一致しないメールを送信する
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Mail:Mode=nontarget

# 重複検証用に同一 Message-Id の対象メールを送信する
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Mail:Mode=duplicate

# 件名などを引数で指定してメールを送信する
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Mail:Mode=custom Mail:Subject=任意件名 Mail:Body=任意本文 Mail:From=sender@example.local Mail:To=test@example.local
```

| モード | 用途 | 件名 / Message-Id |
| --- | --- | --- |
| `target` | バッチの対象条件に一致するメールを投入する | `Mail:TargetSubject` / 自動生成 |
| `nontarget` または `non-target` | バッチの対象条件に一致しないメールを投入する | `Mail:NonTargetSubject` / 自動生成 |
| `duplicate` | 重複検証用に同一 Message-Id の対象メールを投入する | `Mail:TargetSubject` / `Mail:DuplicateMessageId` |
| `custom` | 件名などを引数で指定してメールを投入する | `Mail:Subject` / 自動生成 |

## メールボックス確認方法

開発用メールサーバには GreenMail Standalone を採用します。SMTP と IMAP を同一コンテナで提供できるため、`TestMailSender` からのメール投入と `MailBatch.Console` からの IMAP 取得を Docker Compose 上で検証できます。

| 項目 | ホスト実行時 | Compose 内実行時 |
| --- | --- | --- |
| SMTP | `host.docker.internal:1025` | `mailserver:3025` |
| IMAP | `host.docker.internal:1143` | `mailserver:3143` |
| ユーザー名 | `test@example.local` | `test@example.local` |
| パスワード | `password` | `password` |
| メールボックス | `INBOX` | `INBOX` |

メールボックスの一覧は IMAP 経由で確認します。`curl` が IMAP に対応している環境では、次のコマンドで `INBOX` のメッセージ一覧を確認できます。

```bash
curl --url "imap://host.docker.internal:1143/INBOX" --user "test@example.local:password"
```

特定メールの内容を確認する場合は、一覧で確認したメッセージ番号を指定します。

```bash
curl --url "imap://host.docker.internal:1143/INBOX;MAILINDEX=1" --user "test@example.local:password"
```

SMTP / IMAP のポート疎通だけを確認したい場合は、次のコマンドを利用できます。

```bash
nc -vz host.docker.internal 1025
nc -vz host.docker.internal 1143
```

## バッチ起動方法

`MailBatch.Console` は、IMAP で `INBOX` から対象メールを検索し、抽出したメール情報を `MailReceiver.Api` へ POST します。Docker Compose 起動後、リポジトリルートから次のように実行します。

```bash
dotnet run --project MailBatchSample/src/MailBatch.Console/MailBatch.Console.csproj
```

既定では次の接続先を利用します。

| 用途 | 既定値 |
| --- | --- |
| IMAP | `host.docker.internal:1143` |
| API | `http://host.docker.internal:5000/api/received-mails` |
| 対象件名 | `連携対象` |
| ログ出力先 | `MailBatchSample/logs/` |

バッチ実行後、ログは日次ファイルとして `MailBatchSample/logs/batch-yyyyMMdd.log` に出力されます。

```bash
cat MailBatchSample/logs/batch-$(date +%Y%m%d).log
```

Compose のホスト側公開ポートは `MAILSERVER_SMTP_HOST_PORT` と `MAILSERVER_IMAP_HOST_PORT`、GreenMail の初期ユーザー定義は `MAILSERVER_USERS` で上書きできます。既定値はローカル検証用の固定値であり、本番環境では利用しません。

## 連携先サーバ確認方法

`MailReceiver.Api` の起動確認は `/health` で行います。

```bash
curl http://host.docker.internal:5000/health
```

バッチが連携したメール情報は GET API で確認できます。

```bash
curl http://host.docker.internal:5000/api/received-mails
```

特定 ID のメールだけを確認する場合は、次のように ID を指定します。

```bash
curl http://host.docker.internal:5000/api/received-mails/1
```

## 一連の検証例

```bash
# 1. メールサーバと API を起動する
cd MailBatchSample
docker compose up -d --build
cd ..

# 2. API の起動を確認する
curl http://host.docker.internal:5000/health

# 3. 対象メールを送信する
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Mail:Mode=target

# 4. メールボックスに投入されたことを確認する
curl --url "imap://host.docker.internal:1143/INBOX" --user "test@example.local:password"

# 5. バッチを実行する
dotnet run --project MailBatchSample/src/MailBatch.Console/MailBatch.Console.csproj

# 6. 連携先 API に保存されたことを確認する
curl http://host.docker.internal:5000/api/received-mails
```

## トラブルシュート

### Docker Compose の起動に失敗する

- `docker compose -f MailBatchSample/docker-compose.yml config` で Compose 定義が解釈できることを確認してください。
- SMTP、IMAP、API のホスト側ポートが既に利用されている場合は、次のように公開ポートを変更して起動してください。

```bash
MAILSERVER_SMTP_HOST_PORT=2025 MAILSERVER_IMAP_HOST_PORT=2143 MAILRECEIVER_API_HOST_PORT=5500 docker compose -f MailBatchSample/docker-compose.yml up -d --build
```

ポートを変更した場合は、`TestMailSender`、`MailBatch.Console`、確認用 `curl` の接続先も同じ値に合わせてください。

### API の `/health` が成功しない

- `docker compose -f MailBatchSample/docker-compose.yml ps` で `mailreceiver-api` が起動していることを確認してください。
- `docker compose -f MailBatchSample/docker-compose.yml logs mailreceiver-api` で起動エラーを確認してください。
- `MailBatchSample/data/` に DB ファイルを作成できない場合は、ディレクトリの権限を確認し、必要に応じて `mkdir -p MailBatchSample/data` を実行してください。

### テストメールを送信できない

- `nc -vz host.docker.internal 1025` で SMTP ポートへ接続できることを確認してください。
- Compose の SMTP 公開ポートを変更している場合は、`TESTMAILSENDER_Smtp__Port` またはコマンドライン引数 `Smtp:Port` で `TestMailSender` の接続先を上書きしてください。
- 送信先ユーザーを変更している場合は、`MAILSERVER_USERS` と `Mail:To` の設定が一致していることを確認してください。

### バッチでメールを取得できない

- `curl --url "imap://host.docker.internal:1143/INBOX" --user "test@example.local:password"` でメールボックスにメールが存在することを確認してください。
- 件名が既定の対象条件 `連携対象` を含むことを確認してください。対象外メールを投入した場合、バッチの取得件数は 0 件になります。
- Compose の IMAP 公開ポートを変更している場合は、`MAILBATCH_Imap__Port` またはコマンドライン引数 `Imap:Port` で `MailBatch.Console` の接続先を上書きしてください。

### API 連携または保存結果を確認できない

- `curl http://host.docker.internal:5000/api/received-mails` で API 側に保存済みデータがあるか確認してください。
- `MailBatchSample/logs/batch-$(date +%Y%m%d).log` に API のステータスコードやエラー内容が出力されているか確認してください。
- 同一 `Message-Id` のメールを再処理した場合は、重複として `409 Conflict` が記録されることがあります。これは重複登録を防ぐための想定挙動です。

### 検証データを初期化したい

検証を最初からやり直す場合は、Compose を停止してから実行データを削除します。

```bash
docker compose -f MailBatchSample/docker-compose.yml down
rm -rf MailBatchSample/data MailBatchSample/logs
docker compose -f MailBatchSample/docker-compose.yml up -d --build
```

`data/` と `logs/` はローカル検証用の実行データであり、ソース管理対象には含めません。

## main ブランチ統合時の自動テスト

GitHub Actions の `CI` ワークフローで、`main` ブランチ向け Pull Request 作成・更新時と `main` ブランチへの push 時に `dotnet restore`、`dotnet build`、`dotnet test` を自動実行します。

Pull Request のテスト失敗時にマージできないようにするには、GitHub リポジトリ側で `main` ブランチに対する Branch protection rule または Ruleset を設定し、必須ステータスチェックとして `Build and test` を指定してください。これにより、`CI / Build and test` が成功するまで Pull Request のマージをブロックできます。
