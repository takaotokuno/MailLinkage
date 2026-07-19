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
# メールボックス一覧取得
curl --url "imap://mailserver:3143/INBOX" --user "test@example.local:password"

# メール一覧を取得
curl --url "imap://mailserver:3143/INBOX" \
  --user "test@example.local:password" \
  --request "FETCH 1:* (UID FLAGS INTERNALDATE BODY.PEEK[HEADER.FIELDS (FROM TO SUBJECT DATE MESSAGE-ID)])"
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
| API 失敗メールボックス | `Error` |
| IMAP / API 再試行回数 | 各 3 回（2、4、8 秒待機） |
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
5. API 呼び出しが 2、4、8 秒の待機後に再試行され、最終失敗時は `Error` へ移動して終了コード `2` となることを確認する。

### 重複メール

1. 同一 Message-Id のメール、または同一メールの再処理を発生させる。
2. API が `409 Conflict` を返す、または定義した冪等挙動になることを確認する。
3. バッチログに重複時の API 結果が残ることを確認する。

## 最低限のメトリクスアラート

運用上の異常を見逃さず、過剰なアラートを避けるため、次の3項目に限定して監視する。

- 直近10回のバッチ実行のうち、終了コードが成功以外の実行が50%を超えた場合
- 未復旧のメール移動失敗が7日以上継続した場合
- 直近10回のバッチ実行のうち、処理時間が1時間を超えた実行が50%を超えた場合

3項目目は、成功扱いでも処理遅延やタイムアウト直前の状態が継続する劣化を検知するために追加する。
履歴が10回に満たない間は、母数が少ないことによる誤検知を避けるため履歴ベースのアラートを送信しない。

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

MailBatch.Console は処理済みメール台帳とメール移動失敗を `Batch:LogDirectory` 配下の
`mail-processing.db` に保存する。処理済みメールは `processed_mails`、再移動待ちのメールは
`mail_move_failures` で確認できる。UID はメールボックスの UIDVALIDITY と組み合わせて識別する。
移動失敗が滞留し始めた日時は `created_at_utc`、通常処理または復旧処理で最後に移動に失敗した日時は
`last_failed_at_utc` で確認する。

バッチ終了時にはログと同じ `Batch:LogRetentionDays` を保持期間として、`processed_mails`、
`batch_runs`、`api_execution_results` の保持期限より古いレコードを `DELETE` する。
`mail_move_failures` は二重送信防止に使用するため、保持期間による削除対象には含めず、メール移動の
成功時にのみ削除する。保持期間による削除後は `VACUUM` を実行して未使用領域を回収し、DB ファイルの
肥大化を防ぐ。

```bash
sqlite3 MailBatchSample/logs/mail-processing.db \
  "select uid, uid_validity, processed_at_utc from processed_mails order by processed_at_utc;"
sqlite3 MailBatchSample/logs/mail-processing.db \
  "select uid, uid_validity, destination, created_at_utc, last_failed_at_utc from mail_move_failures order by created_at_utc;"
```

SQLite CLI を利用できる場合は次のように確認する。

```bash
sqlite3 data/mailreceiver.db "select id, message_id, sender, subject, received_at, created_at from received_mails order by id;"
```

GET API で確認する場合は次のように確認する。

```bash
curl http://mailreceiver-api:8080/api/received-mails
```

## 障害調査用の SQLite データ取り出し

障害調査では稼働中の DB ファイルを直接コピーせず、SQLite の `.backup` コマンドで整合性のある
スナップショットを作成してから参照する。調査コマンドはリポジトリルートで実行する。
既定構成で対象となる DB は次の 2 つである。

| DB | ホスト上のパス | 主な内容 |
| --- | --- | --- |
| API DB | `MailBatchSample/data/mailreceiver.db` | API が受信したメール (`received_mails`) |
| バッチ DB | `MailBatchSample/logs/mail-processing.db` | 実行履歴、API 実行結果、処理済みメール、メール移動失敗 |

設定を上書きしている場合、API DB は `ConnectionStrings__MailReceiver`、バッチ DB は
`Batch:LogDirectory`（環境変数では `MAILBATCH_Batch__LogDirectory`）から実際の保存先を確認する。
Docker Compose の既定構成では API コンテナの `/app/data` がホストの `MailBatchSample/data` に
バインドマウントされるため、ホスト側のパスから取り出せる。

### 1. 調査用スナップショットを作成する

`sqlite3` が利用できる端末で、調査 ID やチケット番号を `INCIDENT_ID` に設定して実行する。
`.backup` は SQLite のオンラインバックアップ機能を使うため、API やバッチを停止せずに
コミット済みデータの整合性を保ったコピーを作成できる。

```bash
INCIDENT_ID=INC-YYYYMMDD-001
EVIDENCE_DIR="incident-evidence/${INCIDENT_ID}"
mkdir -p "${EVIDENCE_DIR}"

sqlite3 MailBatchSample/data/mailreceiver.db \
  ".backup '${EVIDENCE_DIR}/mailreceiver.db'"
sqlite3 MailBatchSample/logs/mail-processing.db \
  ".backup '${EVIDENCE_DIR}/mail-processing.db'"

sha256sum "${EVIDENCE_DIR}/mailreceiver.db" \
  "${EVIDENCE_DIR}/mail-processing.db" > "${EVIDENCE_DIR}/SHA256SUMS"
printf 'collected_at_utc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  > "${EVIDENCE_DIR}/collection-info.txt"
```

DB が存在しない場合は `sqlite3` が空の DB を新規作成してしまうため、先に
`test -f <DB のパス>` で対象ファイルの存在を確認する。書き込み元を安全に停止できる場合は、
停止後に DB 本体と同じディレクトリにある `-wal`、`-shm` ファイルを含めてコピーしてもよいが、
通常は `.backup` を優先する。作成した `incident-evidence/` は調査用成果物であり、Git へ
コミットしない。

### 2. スナップショットの整合性と構造を確認する

```bash
sha256sum -c "${EVIDENCE_DIR}/SHA256SUMS"
sqlite3 "${EVIDENCE_DIR}/mailreceiver.db" "PRAGMA integrity_check;"
sqlite3 "${EVIDENCE_DIR}/mail-processing.db" "PRAGMA integrity_check;"
sqlite3 "${EVIDENCE_DIR}/mailreceiver.db" ".tables"
sqlite3 "${EVIDENCE_DIR}/mail-processing.db" ".tables"
```

`PRAGMA integrity_check;` の結果が `ok` であることを確認する。`ok` 以外の場合は元 DB を
更新・修復せず、スナップショットと SQLite のエラー出力をそのまま保全する。

### 3. 調査対象を検索する

まず `batch_runs` で発生時間帯と実行 ID (`run_id`) を絞り、同じ `run_id` で
`api_execution_results` を検索する。時刻は UTC の ISO 8601 形式で保存されている。
次の例の日時と実行 ID は実際の障害に合わせて置き換える。

```bash
sqlite3 -header -column "${EVIDENCE_DIR}/mail-processing.db" \
  "SELECT * FROM batch_runs
   WHERE started_at_utc >= '2026-07-19T00:00:00Z'
     AND started_at_utc <  '2026-07-20T00:00:00Z'
   ORDER BY started_at_utc;"

sqlite3 -header -column "${EVIDENCE_DIR}/mail-processing.db" \
  "SELECT execution_id, run_id, uid, uid_validity, outcome, status_code,
          saved_id, response_summary, error_type, started_at_utc,
          completed_at_utc, duration_ms
   FROM api_execution_results
   WHERE run_id = '<調査対象の run_id>'
   ORDER BY started_at_utc;"

sqlite3 -header -column "${EVIDENCE_DIR}/mail-processing.db" \
  "SELECT uid, uid_validity, processed_at_utc
   FROM processed_mails ORDER BY processed_at_utc;"
sqlite3 -header -column "${EVIDENCE_DIR}/mail-processing.db" \
  "SELECT uid, uid_validity, destination, created_at_utc, last_failed_at_utc
   FROM mail_move_failures ORDER BY last_failed_at_utc;"

sqlite3 -header -column "${EVIDENCE_DIR}/mailreceiver.db" \
  "SELECT id, message_id, sender, subject, received_at, created_at
   FROM received_mails
   WHERE created_at >= '2026-07-19T00:00:00Z'
     AND created_at <  '2026-07-20T00:00:00Z'
   ORDER BY created_at;"
```

`api_execution_results.saved_id` と `received_mails.id`、またはメールの Message-Id とログを
突き合わせると、バッチ実行から API 保存までを追跡できる。メール本文が必要な場合だけ、対象 ID を
限定して `SELECT id, body FROM received_mails WHERE id = <ID>;` を実行する。

### 4. 共有用 CSV を出力する

調査結果だけを共有する場合は、スナップショットから必要な列と期間に限定して CSV を作成する。

```bash
sqlite3 -header -csv "${EVIDENCE_DIR}/mail-processing.db" \
  "SELECT * FROM batch_runs ORDER BY started_at_utc;" \
  > "${EVIDENCE_DIR}/batch-runs.csv"
sqlite3 -header -csv "${EVIDENCE_DIR}/mail-processing.db" \
  "SELECT * FROM api_execution_results ORDER BY started_at_utc;" \
  > "${EVIDENCE_DIR}/api-execution-results.csv"
sqlite3 -header -csv "${EVIDENCE_DIR}/mailreceiver.db" \
  "SELECT id, message_id, sender, subject, received_at, created_at
   FROM received_mails ORDER BY created_at;" \
  > "${EVIDENCE_DIR}/received-mails.csv"
```

`received_mails.body`、`sender`、`subject` および `api_execution_results.response_summary` には
個人情報・機微情報が含まれる可能性がある。共有先の権限と保存期限を確認し、不要な列や行を除外、
またはマスキングしてから受け渡す。DB スナップショット自体も同じ機密区分で取り扱う。

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
