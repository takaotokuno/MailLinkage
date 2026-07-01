# 実行・検証設計

## ローカル実行の前提

この手順は Dev Container 内で実行することを既定とする。Dev Container 内の `localhost` は Dev Container 自身を指すため、Docker Compose がホスト側に公開した SMTP / IMAP / HTTP ポートへ接続するときは `host.docker.internal` を利用する。ホスト OS のターミナルで実行する場合のみ、`host.docker.internal` を `localhost` に読み替える。

- .NET 8 SDK がインストールされていること。
- Docker と Docker Compose が利用できること。
- リポジトリ配下に `logs/` と `data/` を作成できること。

## 起動手順案

```bash
cd MailBatchSample
docker compose up -d --build
```

起動後に確認する項目は次の通り。

1. GreenMail の SMTP ポートへ `host.docker.internal:1025` で接続できる。
2. GreenMail の IMAP ポートへ `host.docker.internal:1143` で接続できる。
3. テスト用アカウント `test@example.local` / `password` で認証できる。
4. API の `/health` が成功する。
5. `data/mailreceiver.db` が作成される、または API 起動時に作成可能である。

SMTP と IMAP の疎通確認例は次の通り。

```bash
nc -vz host.docker.internal 1025
nc -vz host.docker.internal 1143
```

Dev Container 内で実行するアプリケーションは `host.docker.internal:1025` と `host.docker.internal:1143` を利用する。ホスト OS で直接実行する場合は `localhost:1025` と `localhost:1143` へ上書きする。Docker Compose 内でアプリケーションをコンテナ実行する場合は、同一 Compose ネットワーク上の `mailserver:3025` と `mailserver:3143` を利用する。

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
curl http://host.docker.internal:5000/api/received-mails
```

## 実装時の注意点

- パスワード、接続文字列に含まれる秘匿値はログ出力しない。
- 日時は可能な限り UTC またはタイムゾーン付き ISO 8601 で扱う。
- メール本文は長文になり得るため、ログへ全文を出力しない。
- IMAP 取得後に既読化・削除するかどうかは、検証シナリオに合わせて実装時に明示する。
- Docker Compose からの接続先とホスト実行時の接続先が異なるため、設定で切り替えられるようにする。


## メールサーバ設定

開発用メールサーバは GreenMail Standalone を利用する。Docker Compose では SMTP と IMAP を有効にし、初期アカウントとして `test@example.local` / `password` を作成する。メール投入先および IMAP 取得対象の既定メールボックスは `INBOX` とする。

| 実行場所 | SMTP 接続先 | IMAP 接続先 | 認証情報 |
| --- | --- | --- | --- |
| Dev Container | `host.docker.internal:1025` | `host.docker.internal:1143` | `test@example.local` / `password` |
| ホスト OS | `localhost:1025` | `localhost:1143` | `test@example.local` / `password` |
| Compose 内コンテナ | `mailserver:3025` | `mailserver:3143` | `test@example.local` / `password` |

ホスト側公開ポートを変更したい場合は、Compose 起動時に `MAILSERVER_SMTP_HOST_PORT` または `MAILSERVER_IMAP_HOST_PORT` を指定する。ユーザー定義を変更したい場合は、GreenMail のユーザー定義形式で `MAILSERVER_USERS` を指定する。例: `MAILSERVER_USERS=another:secret@example.local`。


## トラブルシュート

### Docker Compose の起動に失敗する

- `docker compose -f MailBatchSample/docker-compose.yml config` で Compose 定義を検証する。
- ホスト側の `1025`、`1143`、`5000` が使用中の場合は、`MAILSERVER_SMTP_HOST_PORT`、`MAILSERVER_IMAP_HOST_PORT`、`MAILRECEIVER_API_HOST_PORT` で公開ポートを変更する。
- ポートを変更した場合は、ホストから実行する `TestMailSender`、`MailBatch.Console`、確認用コマンドの接続先も変更後の値へ合わせる。

### API の起動確認に失敗する

- `docker compose -f MailBatchSample/docker-compose.yml ps` で API コンテナの状態を確認する。
- `docker compose -f MailBatchSample/docker-compose.yml logs mailreceiver-api` で起動時例外や SQLite の作成エラーを確認する。
- `MailBatchSample/data/` が作成できない場合は、ホスト側ディレクトリの作成権限を確認する。

### SMTP / IMAP に接続できない

- SMTP は `nc -vz host.docker.internal 1025`、IMAP は `nc -vz host.docker.internal 1143` で疎通を確認する。
- GreenMail の初期ユーザーを変更している場合は、`MAILSERVER_USERS`、SMTP 送信設定、IMAP 取得設定のユーザー名とパスワードが一致していることを確認する。
- Dev Container 内から接続する場合は `localhost` ではなく `host.docker.internal:1025` と `host.docker.internal:1143` を利用する。Compose 内コンテナから接続する場合は `mailserver:3025` と `mailserver:3143` を利用する。

### バッチの取得件数が 0 件になる

- `curl --url "imap://host.docker.internal:1143/INBOX" --user "test@example.local:password"` で対象メールが `INBOX` に存在することを確認する。
- 件名に検索条件の既定値 `連携対象` が含まれることを確認する。
- 検索条件や既読・フラグの扱いを変更している場合は、設定値とメールの状態が一致していることを確認する。

### API 連携に失敗する

- `curl http://host.docker.internal:5000/health` で API が起動していることを確認する。
- バッチログ `MailBatchSample/logs/batch-yyyyMMdd.log` の HTTP ステータスコードとエラー内容を確認する。
- 同一 `Message-Id` の再処理で `409 Conflict` が返る場合は、重複登録防止として想定された挙動である。

### 検証データを初期化する

```bash
docker compose -f MailBatchSample/docker-compose.yml down
rm -rf MailBatchSample/data MailBatchSample/logs
docker compose -f MailBatchSample/docker-compose.yml up -d --build
```

`data/` と `logs/` はローカル検証用の実行データのため、初期化すると保存済みメールとバッチログは削除される。
