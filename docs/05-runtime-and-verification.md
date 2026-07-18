# 実行・検証設計

## ローカル実行の前提

この手順は Dev Container 内で実行することを既定とする。Dev Container は Docker Compose サービスとして検証用サービスと同じ Compose ネットワークに参加しているため、`mailserver` と `mailreceiver-api` のサービス名を利用する。Dev Container 内の `localhost` は Dev Container 自身を指すため、ホスト OS のターミナルで実行する場合のみ、Compose が公開した `localhost` のポートを利用する。

- .NET 8 SDK がインストールされていること。
- Docker と Docker Compose が利用できること。
- リポジトリ配下に `logs/` と `data/` を作成できること。

## 起動手順案

```bash
docker compose -f MailBatchSample/docker-compose.yml up -d --build mailserver mailreceiver-api
```

起動後に確認する項目は次の通り。

1. GreenMail の SMTP ポートへ `mailserver:3025` で接続できる。
2. GreenMail の IMAP ポートへ `mailserver:3143` で接続できる。
3. テスト用アカウント `test@example.local` / `password` で認証できる。
4. API の `/health` が成功する。
5. `data/mailreceiver.db` が作成される、または API 起動時に作成可能である。

SMTP と IMAP の疎通確認例は次の通り。

```bash
nc -vz mailserver 3025
nc -vz mailserver 3143
```

Dev Container 内で実行するアプリケーションは、同一 Compose ネットワーク上の `mailserver:3025` と `mailserver:3143` を利用する。ホスト OS で直接実行する場合は `localhost:1025` と `localhost:1143` へ上書きする。


## テストメール送信

Docker Compose 起動後、リポジトリルートから `TestMailSender` を実行して検証用メールを投入する。`TestMailSender` は `MailBatchSample/src/TestMailSender/appsettings.json` の SMTP 接続設定とメール既定値を読み込み、`TESTMAILSENDER_` プレフィックスの環境変数またはコマンドライン引数で上書きできる。

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

## メールボックス確認

メールボックスの一覧は IMAP 経由で確認する。`curl` が IMAP に対応している環境では、次のコマンドで `INBOX` のメッセージ一覧を確認できる。

```bash
curl --url "imap://mailserver:3143/INBOX" --user "test@example.local:password"
```

特定メールの内容を確認する場合は、一覧で確認したメッセージ番号を指定する。

```bash
curl --url "imap://mailserver:3143/INBOX;MAILINDEX=1" --user "test@example.local:password"
```

## バッチ起動と結果確認

`MailBatch.Console` は、IMAP で `INBOX` から対象メールを検索し、抽出したメール情報を `MailReceiver.Api` へ POST する。Docker Compose 起動後、リポジトリルートから次のように実行する。コンテナ動作確認では Azure Key Vault は不要で、`MAILBATCH_` プレフィックス付き環境変数（例: `MAILBATCH_Imap__Password`）やコマンドライン引数でシークレットを供給できる。Azure Key Vault を利用する場合だけ `AzureKeyVault:Enabled=true` と `AzureKeyVault:VaultUri` を設定する。

```bash
dotnet run --project MailBatchSample/src/MailBatch.Console/MailBatch.Console.csproj
```

既定では次の接続先を利用する。

| 用途 | 既定値 |
| --- | --- |
| IMAP | `mailserver:3143` |
| API | `http://mailreceiver-api:8080/api/received-mails` |
| 対象件名 | `連携対象` |
| 対象送信元 | `sender@example.local` |
| 処理済みメールボックス | `Processed` |
| ログ出力先 | `MailBatchSample/logs/` |

バッチ実行後、ログは日次ファイルとして `MailBatchSample/logs/batch-yyyyMMdd.log` に出力される。

```bash
cat MailBatchSample/logs/batch-$(date +%Y%m%d).log
```

## 検証シナリオ

### 正常系

1. `TestMailSender` で件名に対象条件を含むメールを送信する。
2. `MailBatch.Console` を実行する。
3. バッチログに処理開始、取得件数、API 連携成功、処理終了が出力されることを確認する。
4. `GET /api/received-mails` で保存済みメールを確認する。
5. SQLite DB の `received_mails` テーブルにレコードが保存されていることを確認する。

### 対象外メール

1. `TestMailSender` で対象条件に一致しない件名のメールを送信する。
2. `MailBatch.Console` を実行する。
3. バッチログの取得件数が 0 件、または対象外として扱われることを確認する。
4. API に POST されないことを確認する。

### API 停止時

1. API を停止する。
2. 対象メールを投入する。
3. `MailBatch.Console` を実行する。
4. API 連携失敗がログに出力されることを確認する。
5. 初期スコープでは複雑なリトライは行わず、失敗をログで追跡できることを確認する。

### 重複メール

1. 同一 Message-Id のメール、または同一メールの再処理を発生させる。
2. API が `409 Conflict` を返す、または定義した冪等挙動になることを確認する。
3. バッチログに重複時の API 結果が残ることを確認する。

## ログ確認

バッチログは日次ファイルとして次の形式で出力する。

```text
logs/batch-yyyyMMdd.log
```

ログには処理証跡として最低限、次の情報を含める。

- 実行日時
- 処理単位を追跡するための実行 ID
- 対象メール件数
- 各メールの Message-Id
- API ステータスコード
- エラー内容

## DB 確認

SQLite CLI を利用できる場合は次のように確認する。

```bash
sqlite3 data/mailreceiver.db "select id, message_id, sender, subject, received_at, created_at from received_mails order by id;"
```

GET API で確認する場合は次のように確認する。

```bash
curl http://mailreceiver-api:8080/api/received-mails
```

## 実装時の注意点

- パスワード、接続文字列に含まれる秘匿値はログ出力しない。
- 日時は可能な限り UTC またはタイムゾーン付き ISO 8601 で扱う。
- メール本文は長文になり得るため、ログへ全文を出力しない。
- API 連携に成功したメールは、再処理を避けるため処理済みメールボックスへ移動する。
- Docker Compose からの接続先とホスト実行時の接続先が異なるため、設定で切り替えられるようにする。


## メールサーバ設定

開発用メールサーバは GreenMail Standalone を利用する。Docker Compose では SMTP と IMAP を有効にし、初期アカウントとして `test@example.local` / `password` を作成する。メール投入先および IMAP 取得対象の既定メールボックスは `INBOX` とする。

| 実行場所 | SMTP 接続先 | IMAP 接続先 | 認証情報 |
| --- | --- | --- | --- |
| Dev Container / Compose 内コンテナ | `mailserver:3025` | `mailserver:3143` | `test@example.local` / `password` |
| ホスト OS | `localhost:1025` | `localhost:1143` | `test@example.local` / `password` |

ホスト側公開ポートを変更したい場合は、Compose 起動時に `MAILSERVER_SMTP_HOST_PORT` または `MAILSERVER_IMAP_HOST_PORT` を指定する。ユーザー定義を変更したい場合は、GreenMail のユーザー定義形式で `MAILSERVER_USERS` を指定する。例: `MAILSERVER_USERS=another:secret@example.local`。


## トラブルシュート

### Docker Compose の起動に失敗する

- `docker compose -f MailBatchSample/docker-compose.yml config` で Compose 定義を検証する。
- ホスト側の `1025`、`1143`、`5000` が使用中の場合は、`MAILSERVER_SMTP_HOST_PORT`、`MAILSERVER_IMAP_HOST_PORT`、`MAILRECEIVER_API_HOST_PORT` で公開ポートを変更する。
- ポートを変更した場合は、ホストから実行する `TestMailSender`、`MailBatch.Console`、確認用コマンドの接続先も変更後の値へ合わせる。Dev Container 内で Compose サービス名を使う場合は、ホスト側公開ポートの変更に追従する必要はない。

### API の起動確認に失敗する

- `docker compose -f MailBatchSample/docker-compose.yml ps` で API コンテナの状態を確認する。
- `docker compose -f MailBatchSample/docker-compose.yml logs mailreceiver-api` で起動時例外や SQLite の作成エラーを確認する。
- `MailBatchSample/data/` が作成できない場合は、ホスト側ディレクトリの作成権限を確認する。

### SMTP / IMAP に接続できない

- SMTP は `nc -vz mailserver 3025`、IMAP は `nc -vz mailserver 3143` で疎通を確認する。
- GreenMail の初期ユーザーを変更している場合は、`MAILSERVER_USERS`、SMTP 送信設定、IMAP 取得設定のユーザー名とパスワードが一致していることを確認する。
- Dev Container 内から接続する場合は `localhost` ではなく `mailserver:3025` と `mailserver:3143` を利用する。ホスト OS から接続する場合は `localhost:1025` と `localhost:1143` を利用する。

### バッチの取得件数が 0 件になる

- `curl --url "imap://mailserver:3143/INBOX" --user "test@example.local:password"` で対象メールが `INBOX` に存在することを確認する。
- 件名に検索条件の既定値 `連携対象` が含まれることを確認する。
- 検索条件や処理済みメールボックスへの移動設定を変更している場合は、設定値とメールの所在が一致していることを確認する。

### API 連携に失敗する

- `curl http://mailreceiver-api:8080/health` で API が起動していることを確認する。
- バッチログ `MailBatchSample/logs/batch-yyyyMMdd.log` の HTTP ステータスコードとエラー内容を確認する。
- 同一 `Message-Id` の再処理で `409 Conflict` が返る場合は、重複登録防止として想定された挙動である。

### 検証データを初期化する

```bash
docker compose -f MailBatchSample/docker-compose.yml stop mailserver mailreceiver-api
docker compose -f MailBatchSample/docker-compose.yml rm -f mailserver mailreceiver-api
rm -rf MailBatchSample/data MailBatchSample/logs
docker compose -f MailBatchSample/docker-compose.yml up -d --build mailserver mailreceiver-api
```

`data/` と `logs/` はローカル検証用の実行データのため、初期化すると保存済みメールとバッチログは削除される。

## main ブランチ統合時の自動テスト

GitHub Actions の `CI` ワークフローで、`main` ブランチ向け Pull Request 作成・更新時と `main` ブランチへの push 時に `dotnet restore`、`dotnet build`、`dotnet test` を自動実行する。

Pull Request のテスト失敗時にマージできないようにするには、GitHub リポジトリ側で `main` ブランチに対する Branch protection rule または Ruleset を設定し、必須ステータスチェックとして `Build and test` を指定する。これにより、`CI / Build and test` が成功するまで Pull Request のマージをブロックできる。
