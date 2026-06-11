# MailLinkage

MailLinkage は、Docker 上に構築したテスト用メールサーバに投入されたメールを .NET 8 のバッチアプリで IMAP 取得し、抽出したメール情報を連携用 API へ POST する検証用サンプルです。

初期スコープでは、処理結果を後から確認できる証跡を残すことを重視し、API は SQLite への保存と GET API による確認を提供します。認証認可、CRUD 画面、添付ファイル処理、複雑なリトライ制御は対象外です。

## ドキュメント

- [要件・スコープ](docs/01-requirements-and-scope.md)
- [構成設計](docs/02-architecture.md)
- [アプリケーション設計](docs/03-application-design.md)
- [データ・API 設計](docs/04-data-and-api-design.md)
- [実行・検証設計](docs/05-runtime-and-verification.md)
- [タスクリスト](docs/99-task-list.md)

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
