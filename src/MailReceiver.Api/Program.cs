using MailReceiver.Api.Contracts;
using MailReceiver.Api.Data;
using MailReceiver.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

const int MessageIdMaxLength = 255;
const int SenderMaxLength = 320;
const int SubjectMaxLength = 500;

var builder = WebApplication.CreateBuilder(args);

ConfigureLogging(builder.Logging);
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

ConfigureMiddleware(app);
await InitializeDatabaseAsync(app);
MapEndpoints(app);

app.Run();

static void ConfigureLogging(ILoggingBuilder logging)
{
    logging.ClearProviders();
    logging.AddJsonConsole();
}

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddProblemDetails();
    services.AddDbContext<MailReceiverDbContext>(options => ConfigureSqlite(options, configuration));
}

static void ConfigureSqlite(DbContextOptionsBuilder options, IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("MailReceiver")
        ?? throw new InvalidOperationException("ConnectionStrings:MailReceiver is not configured.");

    options.UseSqlite(connectionString);
}

static void ConfigureMiddleware(WebApplication app)
{
    app.UseExceptionHandler();
}

static void MapEndpoints(WebApplication app)
{
    MapHealthCheckEndpoint(app);
    MapReceivedMailEndpoints(app);
}

static void MapHealthCheckEndpoint(WebApplication app)
{
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
        .WithName("HealthCheck");
}

static void MapReceivedMailEndpoints(WebApplication app)
{
    var receivedMails = app.MapGroup("/api/received-mails")
        .WithTags("ReceivedMails");

    MapCreateReceivedMailEndpoint(receivedMails);
    MapListReceivedMailsEndpoint(receivedMails);
    MapGetReceivedMailByIdEndpoint(receivedMails);
}

static void MapCreateReceivedMailEndpoint(RouteGroupBuilder receivedMails)
{
    receivedMails.MapPost(string.Empty, CreateReceivedMailAsync)
        .WithName("CreateReceivedMail")
        .Produces<ReceivedMailResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status409Conflict);
}

static void MapListReceivedMailsEndpoint(RouteGroupBuilder receivedMails)
{
    receivedMails.MapGet(string.Empty, ListReceivedMailsAsync)
        .WithName("ListReceivedMails")
        .Produces<IReadOnlyList<ReceivedMailResponse>>();
}

static void MapGetReceivedMailByIdEndpoint(RouteGroupBuilder receivedMails)
{
    receivedMails.MapGet("/{id:long}", GetReceivedMailByIdAsync)
        .WithName("GetReceivedMailById")
        .Produces<ReceivedMailResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);
}

static async Task<Ok<List<ReceivedMailResponse>>> ListReceivedMailsAsync(
    MailReceiverDbContext dbContext,
    CancellationToken cancellationToken)
{
    var mails = await dbContext.ReceivedMails
        .AsNoTracking()
        .OrderBy(mail => mail.Id)
        .Select(mail => ToResponse(mail))
        .ToListAsync(cancellationToken);

    return TypedResults.Ok(mails);
}

static async Task<Results<Ok<ReceivedMailResponse>, NotFound<ProblemDetails>>> GetReceivedMailByIdAsync(
    long id,
    MailReceiverDbContext dbContext,
    CancellationToken cancellationToken)
{
    var mail = await dbContext.ReceivedMails
        .AsNoTracking()
        .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

    if (mail is null)
    {
        return TypedResults.NotFound(new ProblemDetails
        {
            Title = "Received mail was not found.",
            Detail = $"Received mail id '{id}' does not exist.",
            Status = StatusCodes.Status404NotFound
        });
    }

    return TypedResults.Ok(ToResponse(mail));
}

static async Task<Results<CreatedAtRoute<ReceivedMailResponse>, ValidationProblem, Conflict<ProblemDetails>>> CreateReceivedMailAsync(
    CreateReceivedMailRequest request,
    MailReceiverDbContext dbContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
{
    var logger = loggerFactory.CreateLogger("ReceivedMails");
    var validationErrors = Validate(request, out var normalizedRequest, out var receivedAt);
    if (validationErrors.Count > 0)
    {
        logger.LogWarning("Received mail request validation failed. MessageId: {MessageId}, ErrorFields: {ErrorFields}",
            request.MessageId,
            validationErrors.Keys);
        return TypedResults.ValidationProblem(validationErrors);
    }

    if (await dbContext.ReceivedMails.AnyAsync(mail => mail.MessageId == normalizedRequest.MessageId, cancellationToken))
    {
        logger.LogInformation("Duplicate received mail was rejected. MessageId: {MessageId}", normalizedRequest.MessageId);
        return TypedResults.Conflict(CreateDuplicateProblemDetails(normalizedRequest.MessageId));
    }

    var receivedMail = new ReceivedMail
    {
        MessageId = normalizedRequest.MessageId,
        Sender = normalizedRequest.Sender,
        Subject = normalizedRequest.Subject,
        Body = normalizedRequest.Body,
        ReceivedAt = receivedAt,
        CreatedAt = DateTimeOffset.UtcNow
    };

    dbContext.ReceivedMails.Add(receivedMail);

    try
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
    {
        logger.LogInformation(exception, "Duplicate received mail was rejected by unique constraint. MessageId: {MessageId}", normalizedRequest.MessageId);
        return TypedResults.Conflict(CreateDuplicateProblemDetails(normalizedRequest.MessageId));
    }

    logger.LogInformation(
        "Received mail was saved. Id: {ReceivedMailId}, MessageId: {MessageId}, Sender: {Sender}, ReceivedAt: {ReceivedAt}",
        receivedMail.Id,
        receivedMail.MessageId,
        receivedMail.Sender,
        receivedMail.ReceivedAt);

    return TypedResults.CreatedAtRoute(
        ToResponse(receivedMail),
        "GetReceivedMailById",
        new { id = receivedMail.Id });
}

static Dictionary<string, string[]> Validate(
    CreateReceivedMailRequest request,
    out NormalizedCreateReceivedMailRequest normalizedRequest,
    out DateTimeOffset receivedAt)
{
    var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    var messageId = request.MessageId?.Trim() ?? string.Empty;
    var sender = request.Sender?.Trim() ?? string.Empty;
    var subject = request.Subject?.Trim() ?? string.Empty;
    var receivedAtText = request.ReceivedAt?.Trim() ?? string.Empty;

    AddRequiredAndLengthErrors(errors, nameof(request.MessageId), messageId, MessageIdMaxLength);
    AddRequiredAndLengthErrors(errors, nameof(request.Sender), sender, SenderMaxLength);
    AddRequiredAndLengthErrors(errors, nameof(request.Subject), subject, SubjectMaxLength);

    if (!errors.ContainsKey(nameof(request.Sender)) && !IsPlausibleEmailAddress(sender))
    {
        errors[nameof(request.Sender)] = ["The sender field must be an email-like address."];
    }

    if (string.IsNullOrWhiteSpace(receivedAtText))
    {
        errors[nameof(request.ReceivedAt)] = ["The receivedAt field is required."];
        receivedAt = default;
    }
    else if (!DateTimeOffset.TryParse(receivedAtText, out receivedAt))
    {
        errors[nameof(request.ReceivedAt)] = ["The receivedAt field must be a valid date and time."];
    }

    normalizedRequest = new NormalizedCreateReceivedMailRequest(
        messageId,
        sender,
        subject,
        string.IsNullOrEmpty(request.Body) ? null : request.Body);

    return errors;
}

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

static bool IsPlausibleEmailAddress(string value)
{
    var atSignIndex = value.IndexOf('@');
    return atSignIndex > 0 && atSignIndex < value.Length - 1;
}

static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
    exception.InnerException is SqliteException { SqliteErrorCode: 19 };

static ProblemDetails CreateDuplicateProblemDetails(string messageId) => new()
{
    Title = "Received mail already exists.",
    Detail = $"A received mail with messageId '{messageId}' already exists.",
    Status = StatusCodes.Status409Conflict
};

static ReceivedMailResponse ToResponse(ReceivedMail mail) => new(
    mail.Id,
    mail.MessageId,
    mail.Sender,
    mail.Subject,
    mail.Body,
    mail.ReceivedAt,
    mail.CreatedAt);

static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialization");
    var connectionString = configuration.GetConnectionString("MailReceiver")
        ?? throw new InvalidOperationException("ConnectionStrings:MailReceiver is not configured.");

    EnsureSqliteDirectoryExists(connectionString, logger);

    var dbContext = scope.ServiceProvider.GetRequiredService<MailReceiverDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    logger.LogInformation("MailReceiver database was initialized.");
}

static void EnsureSqliteDirectoryExists(string connectionString, ILogger logger)
{
    var builder = new SqliteConnectionStringBuilder(connectionString);
    if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    var directory = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
    if (string.IsNullOrEmpty(directory) || Directory.Exists(directory))
    {
        return;
    }

    Directory.CreateDirectory(directory);
    logger.LogInformation("SQLite database directory was created. Directory: {Directory}", directory);
}
