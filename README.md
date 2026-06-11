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

## `logs/` と `data/` の配置方針

`logs/` と `data/` は、`MailBatchSample/` 直下に配置します。ソースコードや Docker Compose 定義から分離し、ローカル実行・Docker Compose 実行のどちらでも同じホスト側ディレクトリを参照できるようにします。

| パス | 用途 | 配置・運用方針 |
| --- | --- | --- |
| `MailBatchSample/logs/` | `MailBatch.Console` のバッチログ出力先 | バッチ実行時に日次ファイル `batch-yyyyMMdd.log` を出力します。ログは実行ごとの証跡として扱い、ソース管理対象には含めません。 |
| `MailBatchSample/data/` | `MailReceiver.Api` が利用する SQLite DB の配置先 | API 起動時または初期化時に `mailreceiver.db` を作成します。DB ファイルはローカル検証用の実行データとして扱い、ソース管理対象には含めません。 |

実行前にディレクトリが存在しない場合は、アプリケーションまたは起動手順で作成します。コンテナから利用する場合は、上記ホスト側ディレクトリをコンテナ内のログ出力先・DB 配置先へボリュームマウントします。
