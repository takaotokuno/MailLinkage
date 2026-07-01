# タスクリスト

## 1. プロジェクト土台

- [x] `MailBatchSample/` ディレクトリを作成する。
- [x] `MailBatchSample/docker-compose.yml` を作成する。
- [x] `src/MailBatch.Console` に .NET 8 Console プロジェクトを作成する。
- [x] `src/MailReceiver.Api` に ASP.NET Core Web API プロジェクトを作成する。
- [x] `src/TestMailSender` に .NET 8 Console プロジェクトを作成する。
- [x] ソリューションファイルを作成し、3 プロジェクトを追加する。
- [x] `logs/` と `data/` の配置方針を README に反映する。

## 2. Docker / メールサーバ

- [x] IMAP 対応の開発用メールサーバ製品を選定する。
- [x] Docker Compose にメールサーバサービスを追加する。
- [x] SMTP、IMAP、必要に応じて Web UI のポートを公開する。
- [x] テスト用メールアカウント、メールボックス、認証情報を定義する。
- [x] ホスト実行とコンテナ実行で利用する接続先設定を整理する。

## 3. MailReceiver.Api

- [x] SQLite 接続設定を追加する。
- [x] `received_mails` テーブルまたは Entity Framework Core モデルを作成する。
- [x] DB 初期化またはマイグレーション手順を用意する。
- [x] `POST /api/received-mails` を実装する。
- [x] POST リクエストの入力検証を実装する。
- [x] `messageId` 重複時の挙動を実装する。
- [x] `GET /api/received-mails` を実装する。
- [x] `GET /api/received-mails/{id}` を実装する。
- [x] `/health` を実装する。
- [x] API の構造化ログを設定する。

## 4. MailBatch.Console

- [x] `appsettings.json` と環境変数による設定読み込みを実装する。
- [x] Serilog の日次ファイルログを設定する。
- [x] IMAP 接続処理を実装する。
- [x] 対象メール検索条件を実装する。
- [x] メール本文とヘッダの抽出処理を実装する。
- [x] API 送信用 DTO を実装する。
- [x] `POST /api/received-mails` 呼び出しを実装する。
- [x] API 成功・失敗のログ出力を実装する。
- [x] 例外時のログ出力と終了コードを整理する。
- [x] 処理済みメールの扱いを決定し実装する。例: 既読化、フラグ付与、何もしない。

## 5. TestMailSender

- [x] SMTP 接続設定を追加する。
- [x] 件名、本文、送信者、宛先を設定または引数で指定できるようにする。
- [x] 対象条件に一致するテストメール送信を実装する。
- [x] 対象条件に一致しないテストメール送信を実装する。
- [x] 同一 Message-Id を使った重複検証方法を用意する。

## 6. 検証とテスト

- [x] Docker Compose 起動確認手順を作成する。
- [x] API の health check を確認する。
- [x] TestMailSender からメール投入できることを確認する。
- [x] MailBatch.Console から IMAP 取得できることを確認する。
- [x] API へ POST され SQLite に保存されることを確認する。
- [x] `GET /api/received-mails` で証跡確認できることを確認する。
- [x] `logs/batch-yyyyMMdd.log` に必要なログが出力されることを確認する。
- [x] API 停止時のエラーログを確認する。
- [x] 重複 Message-Id の挙動を確認する。

## 7. ドキュメント

- [x] README に概要、起動手順、検証手順を追記する。
- [x] 設定項目一覧を整理する。
- [x] API 仕様を実装内容に合わせて更新する。
- [x] DB スキーマを実装内容に合わせて更新する。
- [ ] トラブルシュートを追加する。
