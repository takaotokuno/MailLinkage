# 実行・検証設計

## ローカル実行の前提

- .NET 8 SDK がインストールされていること。
- Docker と Docker Compose が利用できること。
- リポジトリ配下に `logs/` と `data/` を作成できること。

## 起動手順案

```bash
cd MailBatchSample
docker compose up -d --build
```

起動後に確認する項目は次の通り。

1. GreenMail の SMTP ポートへ `localhost:1025` で接続できる。
2. GreenMail の IMAP ポートへ `localhost:1143` で接続できる。
3. テスト用アカウント `test@example.local` / `password` で認証できる。
4. API の `/health` が成功する。
5. `data/mailreceiver.db` が作成される、または API 起動時に作成可能である。

SMTP と IMAP の疎通確認例は次の通り。

```bash
nc -vz localhost 1025
nc -vz localhost 1143
```

ホスト実行のアプリケーションは `localhost:1025` と `localhost:1143` を利用する。Docker Compose 内でアプリケーションをコンテナ実行する場合は、同一 Compose ネットワーク上の `mailserver:3025` と `mailserver:3143` を利用する。

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
curl http://localhost:5000/api/received-mails
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
| ホスト OS | `localhost:1025` | `localhost:1143` | `test@example.local` / `password` |
| Compose 内コンテナ | `mailserver:3025` | `mailserver:3143` | `test@example.local` / `password` |

ホスト側公開ポートを変更したい場合は、Compose 起動時に `MAILSERVER_SMTP_HOST_PORT` または `MAILSERVER_IMAP_HOST_PORT` を指定する。ユーザー定義を変更したい場合は、GreenMail のユーザー定義形式で `MAILSERVER_USERS` を指定する。例: `MAILSERVER_USERS=another:secret@example.local`。
