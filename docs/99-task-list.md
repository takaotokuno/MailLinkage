# タスクリスト

## 1. プロジェクト土台

- [ ] `MailBatchSample/` ディレクトリを作成する。
- [ ] `MailBatchSample/docker-compose.yml` を作成する。
- [ ] `src/MailBatch.Console` に .NET 8 Console プロジェクトを作成する。
- [x] `src/MailReceiver.Api` に ASP.NET Core Web API プロジェクトを作成する。
- [ ] `src/TestMailSender` に .NET 8 Console プロジェクトを作成する。
- [ ] ソリューションファイルを作成し、3 プロジェクトを追加する。
- [ ] `logs/` と `data/` の配置方針を README に反映する。

## 2. Docker / メールサーバ

- [ ] IMAP 対応の開発用メールサーバ製品を選定する。
- [ ] Docker Compose にメールサーバサービスを追加する。
- [ ] SMTP、IMAP、必要に応じて Web UI のポートを公開する。
- [ ] テスト用メールアカウント、メールボックス、認証情報を定義する。
- [ ] ホスト実行とコンテナ実行で利用する接続先設定を整理する。

## 3. MailReceiver.Api

- [ ] SQLite 接続設定を追加する。
- [ ] `received_mails` テーブルまたは Entity Framework Core モデルを作成する。
- [ ] DB 初期化またはマイグレーション手順を用意する。
- [ ] `POST /api/received-mails` を実装する。
- [ ] POST リクエストの入力検証を実装する。
- [ ] `messageId` 重複時の挙動を実装する。
- [ ] `GET /api/received-mails` を実装する。
- [ ] `GET /api/received-mails/{id}` を実装する。
- [ ] `/health` を実装する。
- [ ] API の構造化ログを設定する。

## 4. MailBatch.Console

- [ ] `appsettings.json` と環境変数による設定読み込みを実装する。
- [ ] Serilog の日次ファイルログを設定する。
- [ ] IMAP 接続処理を実装する。
- [ ] 対象メール検索条件を実装する。
- [ ] メール本文とヘッダの抽出処理を実装する。
- [ ] API 送信用 DTO を実装する。
- [ ] `POST /api/received-mails` 呼び出しを実装する。
- [ ] API 成功・失敗のログ出力を実装する。
- [ ] 例外時のログ出力と終了コードを整理する。
- [ ] 処理済みメールの扱いを決定し実装する。例: 既読化、フラグ付与、何もしない。

## 5. TestMailSender

- [ ] SMTP 接続設定を追加する。
- [ ] 件名、本文、送信者、宛先を設定または引数で指定できるようにする。
- [ ] 対象条件に一致するテストメール送信を実装する。
- [ ] 対象条件に一致しないテストメール送信を実装する。
- [ ] 同一 Message-Id を使った重複検証方法を用意する。

## 6. 検証とテスト

- [ ] Docker Compose 起動確認手順を作成する。
- [ ] API の health check を確認する。
- [ ] TestMailSender からメール投入できることを確認する。
- [ ] MailBatch.Console から IMAP 取得できることを確認する。
- [ ] API へ POST され SQLite に保存されることを確認する。
- [ ] `GET /api/received-mails` で証跡確認できることを確認する。
- [ ] `logs/batch-yyyyMMdd.log` に必要なログが出力されることを確認する。
- [ ] API 停止時のエラーログを確認する。
- [ ] 重複 Message-Id の挙動を確認する。

## 7. ドキュメント

- [ ] README に概要、起動手順、検証手順を追記する。
- [ ] 設定項目一覧を整理する。
- [ ] API 仕様を実装内容に合わせて更新する。
- [ ] DB スキーマを実装内容に合わせて更新する。
- [ ] トラブルシュートを追加する。
