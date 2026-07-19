using MailReceiver.Api.Contracts;
using MailReceiver.Api.Data;
using MailReceiver.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

const int SQLITE_CONSTRAINT_ERROR_CODE = 19;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

ConfigureLogging(builder.Logging);
ConfigureServices(builder.Services, builder.Configuration);

WebApplication app = builder.Build();

ConfigureMiddleware(app);
await InitializeDatabaseAsync(app);
MapEndpoints(app);

app.Run();

/// <summary>
/// アプリケーションのログ出力プロバイダーを構成します。
/// </summary>
static void ConfigureLogging(ILoggingBuilder logging)
{
    _ = logging.ClearProviders();
    _ = logging.AddJsonConsole();
}

/// <summary>
/// アプリケーションで使用するサービスをDIコンテナーに登録します。
/// </summary>
static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    _ = services.AddProblemDetails();
    _ = services.AddDbContext<MailReceiverDbContext>(options =>
    {
        ConfigureSqlite(options, configuration);
    });
}

/// <summary>
/// MailReceiver用のSQLite接続をEntity Framework Coreに設定します。
/// </summary>
static void ConfigureSqlite(DbContextOptionsBuilder options, IConfiguration configuration)
{
    string connectionString = configuration.GetConnectionString("MailReceiver")
        ?? throw new InvalidOperationException("ConnectionStrings:MailReceiver is not configured.");

    _ = options.UseSqlite(connectionString);
}

/// <summary>
/// HTTPリクエスト処理パイプラインのミドルウェアを構成します。
/// </summary>
static void ConfigureMiddleware(WebApplication app)
{
    _ = app.UseExceptionHandler();
}

/// <summary>
/// MailReceiver APIで公開するエンドポイントを登録します。
/// </summary>
static void MapEndpoints(WebApplication app)
{
    MapHealthCheckEndpoint(app);
    MapReceivedMailEndpoints(app);
}

/// <summary>
/// アプリケーションの稼働状態を返すヘルスチェックエンドポイントを登録します。
/// </summary>
static void MapHealthCheckEndpoint(WebApplication app)
{
    _ = app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
        .WithName("HealthCheck");
}

/// <summary>
/// 受信メールリソースに関するエンドポイントグループを登録します。
/// </summary>
static void MapReceivedMailEndpoints(WebApplication app)
{
    RouteGroupBuilder receivedMails = app.MapGroup("/api/received-mails")
        .WithTags("ReceivedMails");

    MapCreateReceivedMailEndpoint(receivedMails);
    MapListReceivedMailsEndpoint(receivedMails);
    MapGetReceivedMailByIdEndpoint(receivedMails);
}

/// <summary>
/// 受信メールを新規登録するエンドポイントを登録します。
/// </summary>
static void MapCreateReceivedMailEndpoint(RouteGroupBuilder receivedMails)
{
    _ = receivedMails.MapPost(string.Empty, CreateReceivedMailAsync)
        .WithName("CreateReceivedMail")
        .Produces<ReceivedMailResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict);
}

/// <summary>
/// 登録済みの受信メール一覧を取得するエンドポイントを登録します。
/// </summary>
static void MapListReceivedMailsEndpoint(RouteGroupBuilder receivedMails)
{
    _ = receivedMails.MapGet(string.Empty, ListReceivedMailsAsync)
        .WithName("ListReceivedMails")
        .Produces<IReadOnlyList<ReceivedMailResponse>>();
}

/// <summary>
/// 指定されたIDの受信メールを取得するエンドポイントを登録します。
/// </summary>
static void MapGetReceivedMailByIdEndpoint(RouteGroupBuilder receivedMails)
{
    _ = receivedMails.MapGet("/{id:long}", GetReceivedMailByIdAsync)
        .WithName("GetReceivedMailById")
        .Produces<ReceivedMailResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);
}

/// <summary>
/// 登録済みの受信メールをID昇順で取得します。
/// </summary>
static async Task<Ok<List<ReceivedMailResponse>>> ListReceivedMailsAsync(
    MailReceiverDbContext dbContext,
    CancellationToken cancellationToken)
{
    List<ReceivedMail> mails = await dbContext.ReceivedMails
        .AsNoTracking()
        .OrderBy(mail => mail.Id)
        .ToListAsync(cancellationToken);

    List<ReceivedMailResponse> responses = mails
        .Select(ToResponse)
        .ToList();

    return TypedResults.Ok(responses);
}

/// <summary>
/// 指定されたIDに一致する受信メールを取得します。
/// </summary>
static async Task<Results<Ok<ReceivedMailResponse>, NotFound<ProblemDetails>>> GetReceivedMailByIdAsync(
    long id,
    MailReceiverDbContext dbContext,
    CancellationToken cancellationToken)
{
    ReceivedMail? mail = await dbContext.ReceivedMails
        .AsNoTracking()
        .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

    return mail is null
        ? (Results<Ok<ReceivedMailResponse>, NotFound<ProblemDetails>>)TypedResults.NotFound(new ProblemDetails
        {
            Title = "Received mail was not found.",
            Detail = $"Received mail id '{id}' does not exist.",
            Status = StatusCodes.Status404NotFound
        })
        : TypedResults.Ok(ToResponse(mail));
}

/// <summary>
/// 受信メール登録リクエストを検証し、重複がなければデータベースへ保存します。
/// </summary>
static async Task<Results<CreatedAtRoute<ReceivedMailResponse>, ValidationProblem, Conflict<ProblemDetails>>> CreateReceivedMailAsync(
    CreateReceivedMailRequest request,
    MailReceiverDbContext dbContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
{
    ILogger logger = loggerFactory.CreateLogger("ReceivedMails");
    Dictionary<string, string[]> validationErrors = Validate(request, out NormalizedCreateReceivedMailRequest normalizedRequest);
    if (validationErrors.Count > 0)
    {
        logger.LogWarning("Received mail request validation failed. Key: {Key}, ErrorFields: {ErrorFields}",
            request.Key,
            validationErrors.Keys);
        return TypedResults.ValidationProblem(validationErrors);
    }

    if (await dbContext.ReceivedMails.AnyAsync(mail => mail.Key == normalizedRequest.Key, cancellationToken))
    {
        logger.LogInformation("Duplicate received mail was rejected. Key: {Key}", normalizedRequest.Key);
        return TypedResults.Conflict(CreateDuplicateProblemDetails(normalizedRequest.Key));
    }

    ReceivedMail receivedMail = new()
    {
        Key = normalizedRequest.Key,
        Message = normalizedRequest.Message,
        CreatedAt = DateTimeOffset.UtcNow
    };

    _ = dbContext.ReceivedMails.Add(receivedMail);

    try
    {
        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
    {
        logger.LogInformation(exception, "Duplicate received mail was rejected by unique constraint. Key: {Key}", normalizedRequest.Key);
        return TypedResults.Conflict(CreateDuplicateProblemDetails(normalizedRequest.Key));
    }

    logger.LogInformation(
        "Received mail was saved. Id: {ReceivedMailId}, Key: {Key}",
        receivedMail.Id,
        receivedMail.Key);

    return TypedResults.CreatedAtRoute(
        ToResponse(receivedMail),
        "GetReceivedMailById",
        new
        {
            id = receivedMail.Id
        });
}

/// <summary>
/// 受信メール登録リクエストの入力値を検証し、保存用に正規化します。
/// </summary>
static Dictionary<string, string[]> Validate(
    CreateReceivedMailRequest request,
    out NormalizedCreateReceivedMailRequest normalizedRequest)
{
    Dictionary<string, string[]> errors = new(StringComparer.OrdinalIgnoreCase);
    string key = request.Key?.Trim() ?? string.Empty;
    string message = request.Message?.Trim() ?? string.Empty;

    AddRequiredAndLengthErrors(errors, nameof(request.Key), key, ReceivedMail.KEY_MAX_LENGTH);
    AddRequiredAndLengthErrors(errors, nameof(request.Message), message, ReceivedMail.MESSAGE_MAX_LENGTH);

    normalizedRequest = new NormalizedCreateReceivedMailRequest(key, message);

    return errors;
}

/// <summary>
/// 必須入力と最大文字数に関する検証エラーを追加します。
/// </summary>
static void AddRequiredAndLengthErrors(
    Dictionary<string, string[]> errors,
    string fieldName,
    string value,
    int maxLength)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        errors[fieldName] = [$"The {char.ToLowerInvariant(fieldName[0]) + fieldName[1..]} field is required."];
        return;
    }

    if (value.Length > maxLength)
    {
        errors[fieldName] = [$"The {char.ToLowerInvariant(fieldName[0]) + fieldName[1..]} field must be {maxLength} characters or fewer."];
    }
}

/// <summary>
/// データベース更新例外がSQLiteの一意制約違反かどうかを判定します。
/// </summary>
static bool IsUniqueConstraintViolation(DbUpdateException exception)
{
    return exception.InnerException is SqliteException { SqliteErrorCode: SQLITE_CONSTRAINT_ERROR_CODE };
}

/// <summary>
/// Key重複時に返すProblemDetailsを作成します。
/// </summary>
static ProblemDetails CreateDuplicateProblemDetails(string key)
{
    return new()
    {
        Title = "Received mail already exists.",
        Detail = $"A received mail with key '{key}' already exists.",
        Status = StatusCodes.Status409Conflict
    };
}

/// <summary>
/// 受信メールエンティティをAPIレスポンスに変換します。
/// </summary>
static ReceivedMailResponse ToResponse(ReceivedMail mail)
{
    return new(
        mail.Id,
        mail.Key,
        mail.Message,
        mail.CreatedAt);
}

/// <summary>
/// アプリケーション起動時にMailReceiverデータベースを初期化します。
/// </summary>
static async Task InitializeDatabaseAsync(WebApplication app)
{
    using IServiceScope scope = app.Services.CreateScope();
    IConfiguration configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
    string connectionString = configuration.GetConnectionString("MailReceiver")
        ?? throw new InvalidOperationException("ConnectionStrings:MailReceiver is not configured.");

    EnsureSqliteDirectoryExists(connectionString, logger);

    MailReceiverDbContext dbContext = scope.ServiceProvider.GetRequiredService<MailReceiverDbContext>();
    bool databaseDeleted = await dbContext.Database.EnsureDeletedAsync();
    _ = await dbContext.Database.EnsureCreatedAsync();
    logger.LogInformation(
        "MailReceiver database was initialized. ExistingDatabaseDeleted: {ExistingDatabaseDeleted}",
        databaseDeleted);
}

/// <summary>
/// SQLiteデータベースファイルの配置先ディレクトリが存在しない場合に作成します。
/// </summary>
static void EnsureSqliteDirectoryExists(string connectionString, ILogger logger)
{
    SqliteConnectionStringBuilder builder = new(connectionString);
    if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    string? directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
    if (string.IsNullOrEmpty(directory) || Directory.Exists(directory))
    {
        return;
    }

    _ = Directory.CreateDirectory(directory);
    logger.LogInformation("SQLite database directory was created. Directory: {Directory}", directory);
}
