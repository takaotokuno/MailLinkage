# データ・API 設計

## 保存データ

初期スコープでは、API が受信したメール情報を SQLite の `received_mails` テーブルへ保存する。

| カラム | 型 | 必須 | 内容 |
| --- | --- | --- | --- |
| `id` | INTEGER | Yes | 主キー。自動採番。 |
| `message_id` | TEXT | Yes | メールの Message-Id。 |
| `sender` | TEXT | Yes | 送信者。 |
| `subject` | TEXT | Yes | 件名。 |
| `body` | TEXT | No | 本文。 |
| `received_at` | TEXT | Yes | メール受信日時。ISO 8601 形式を推奨。 |
| `created_at` | TEXT | Yes | API が保存した日時。UTC の ISO 8601 形式を推奨。 |

## 重複データの扱い

初期実装では、`message_id` にユニーク制約を設定する。重複 POST 時は検証用途で挙動が分かりやすいように `409 Conflict` を返す。

## DB 初期化

`MailReceiver.Api` は起動時に `ConnectionStrings:MailReceiver` の SQLite 接続文字列を読み込み、DB ファイルの親ディレクトリを作成したうえで Entity Framework Core の `EnsureCreated` により `received_mails` テーブルと `message_id` のユニークインデックスを作成する。

ローカル開発の既定値は `src/MailReceiver.Api/appsettings.json` の `Data Source=../../data/mailreceiver.db` である。Docker など実行ディレクトリが異なる環境では、環境変数 `ConnectionStrings__MailReceiver` で保存先を上書きする。

## POST API

### Request

`POST /api/received-mails`

```json
{
  "messageId": "<202606110001@example.local>",
  "sender": "sender@example.local",
  "subject": "連携対象メール",
  "body": "本文です。",
  "receivedAt": "2026-06-11T10:00:00Z"
}
```

### Validation

| 項目 | ルール |
| --- | --- |
| `messageId` | 必須。最大 255 文字。 |
| `sender` | 必須。最大 320 文字。メールアドレス形式の厳密検証は初期スコープでは簡易でよい。 |
| `subject` | 必須。最大 500 文字。 |
| `body` | 任意。長文を許容する。 |
| `receivedAt` | 必須。日時として解釈可能であること。 |

### Response: 201 Created

```json
{
  "id": 1,
  "messageId": "<202606110001@example.local>",
  "sender": "sender@example.local",
  "subject": "連携対象メール",
  "body": "本文です。",
  "receivedAt": "2026-06-11T10:00:00Z",
  "createdAt": "2026-06-11T10:01:00Z"
}
```

### Response: 400 Bad Request

入力値が不足している、または日時として解釈できない場合に返す。

### Response: 409 Conflict

同一 `messageId` のデータが既に保存されている場合に返す。

## GET API

### 一覧取得

`GET /api/received-mails`

初期実装では全件返却でよい。件数が増える場合に備え、後続タスクで `limit`、`offset`、`receivedFrom`、`receivedTo` などのクエリを検討する。

### 単一取得

`GET /api/received-mails/{id}`

存在しない ID の場合は `404 Not Found` を返す。

## エラー応答

API のエラー応答は ASP.NET Core 標準の `ProblemDetails` を基本とする。バッチ側は HTTP ステータスコード、レスポンス本文の要約、対象 `messageId` をログへ出力する。
