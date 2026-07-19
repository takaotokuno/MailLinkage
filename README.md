# MailLinkage

MailLinkage は、Docker 上に構築したテスト用メールサーバに投入されたメールを .NET 8 のバッチアプリで IMAP 取得し、抽出したメール情報を連携用 API へ POST する検証用サンプルです。

バッチは bounded channel で取得と送信を並行化し、IMAP / API の一時障害を再試行します。処理台帳・実行履歴・通知メールを残し、API は SQLite への保存と GET API による確認を提供します。認証認可、CRUD 画面、添付ファイル処理は対象外です。

## ドキュメント

詳細な設計・手順は `docs/` に分割しています。

- [開発環境](docs/00-development-environment.md): Dev Container の起動方法、接続先の考え方、よく使う開発コマンド。
- [要件・スコープ](docs/01-requirements-and-scope.md): 目的、対象機能、初期スコープ外、成功条件。
- [構成設計](docs/02-architecture.md): 全体構成、コンテナ構成、通信、設定管理、ディレクトリ方針。
- [アプリケーション設計](docs/03-application-design.md): バッチ、API、テストメール送信アプリの責務と処理方針。
- [データ・API 設計](docs/04-data-and-api-design.md): SQLite の保存データ、重複制御、POST / GET API。
- [実行・検証設計](docs/05-runtime-and-verification.md): 起動手順、検証シナリオ、トラブルシュート、CI。

## クイックスタート

Dev Container 内でリポジトリルートから次の順に実行すると、メール投入から API 保存結果の確認までを一通り検証できます。

```bash
# 1. メールサーバと API を起動する
docker compose -f MailBatchSample/docker-compose.yml up -d --build mailserver mailreceiver-api

# 2. API の起動を確認する
curl http://mailreceiver-api:8080/health

# 3. 対象メールを送信する
dotnet run --project MailBatchSample/src/TestMailSender/TestMailSender.csproj -- Mail:Mode=target

# 4. バッチを実行する
dotnet run --project MailBatchSample/src/MailBatch.Console/MailBatch.Console.csproj

# 5. 連携先 API に保存されたことを確認する
curl http://mailreceiver-api:8080/api/received-mails
```

ホスト OS から実行する場合やポートを変更する場合は、[開発環境](docs/00-development-environment.md) と [実行・検証設計](docs/05-runtime-and-verification.md) を参照してください。

## プロジェクト構成

```text
MailBatchSample/
  docker-compose.yml

  src/
    MailBatch.Console/    # IMAP 取得と API 連携を行うバッチアプリ
    MailReceiver.Api/     # POST 受信、SQLite 保存、GET 確認を行う API
    TestMailSender/       # SMTP でテストメールを投入する補助アプリ

  logs/                   # バッチログ出力先（ソース管理対象外）
  data/                   # SQLite DB 配置先（ソース管理対象外）
```

`logs/` と `data/` の配置・運用方針は [構成設計](docs/02-architecture.md) と [実行・検証設計](docs/05-runtime-and-verification.md) を参照してください。
