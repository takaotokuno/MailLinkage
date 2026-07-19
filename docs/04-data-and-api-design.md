# データ・API 設計

## 保存データ

初期スコープでは、API が受信したメール情報を SQLite の `received_mails` テーブルへ保存する。

バッチ側は API 実行後に確定した事実を `mail-processing.db` の `api_execution_results` へ保存する。対象メール、実行・バッチ ID、エンドポイント、成否、HTTP ステータス、保存済み ID、失敗レスポンスの要約、例外種別、開始・完了日時、所要時間を検索できる。機微情報を含み得るリクエスト本文とレスポンス全文は DB に保存しない。

一方、ログは事実に至る経緯の調査に用いる。API 呼び出しの開始、実行 ID、対象メール ID、エンドポイント、本文の長さ、HTTP ステータス、結果の要約、および例外スタックを記録し、本文そのものは出力しない。DB レコードとログは実行 ID で突合できる。

`api_execution_results` は `Batch:LogRetentionDays` を保持期間としてバッチ終了時に古いレコードを削除し、削除後に `VACUUM` で領域を回収する。

| カラム | 型 | 必須 | 内容 |
| --- | --- | --- | --- |
| `id` | INTEGER | Yes | 主キー。自動採番。 |
| `key` | TEXT | Yes | メール本文から抽出した連携キー。 |
| `message` | TEXT | Yes | 件名と本文から生成した連携メッセージ。 |
| `created_at` | TEXT | Yes | API が保存した日時。UTC の ISO 8601 形式を推奨。 |

## 重複データの扱い

初期実装では、`key` にユニーク制約を設定する。重複 POST 時は検証用途で挙動が分かりやすいように `409 Conflict` を返す。

## DB 初期化

`MailReceiver.Api` は起動時に `ConnectionStrings:MailReceiver` の SQLite 接続文字列を読み込み、DB ファイルの親ディレクトリを作成する。その後、既存の SQLite DB を `EnsureDeleted` で削除し、`EnsureCreated` により `received_mails` テーブルと `key` のユニークインデックスを新規作成する。この検証用 API では、API コンテナを起動するたびに以前の保存データが消去される。

ローカル開発の既定値は `src/MailReceiver.Api/appsettings.json` の `Data Source=../../data/mailreceiver.db` である。Docker など実行ディレクトリが異なる環境では、環境変数 `ConnectionStrings__MailReceiver` で保存先を上書きする。

## POST API

### Request

`POST /api/received-mails`

```json
{
  "key": "ABC123",
  "message": "連携対象メール\n\n本文です。"
}
```

### Validation

| 項目 | ルール |
| --- | --- |
| `key` | 必須。最大 255 文字。 |
| `message` | 必須。最大 500 文字。 |

### Response: 201 Created

```json
{
  "id": 1,
  "key": "ABC123",
  "message": "連携対象メール\n\n本文です。",
  "createdAt": "2026-06-11T10:01:00Z"
}
```

### Response: 400 Bad Request

入力値が不足している、または最大文字数を超える場合に返す。

### Response: 409 Conflict

同一 `key` のデータが既に保存されている場合に返す。

## GET API

### 一覧取得

`GET /api/received-mails`

初期実装では全件返却でよい。件数が増える場合に備え、後続タスクで `limit`、`offset`、`receivedFrom`、`receivedTo` などのクエリを検討する。

### 単一取得

`GET /api/received-mails/{id}`

存在しない ID の場合は `404 Not Found` を返す。

## エラー応答

API のエラー応答は ASP.NET Core 標準の `ProblemDetails` を基本とする。バッチ側は HTTP ステータスコード、レスポンス本文の要約、対象メール ID をログへ出力する。
