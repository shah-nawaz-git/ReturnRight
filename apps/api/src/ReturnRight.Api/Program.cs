using System.Text.Json.Serialization;
using FluentValidation;
using ReturnRight.Api.Email;
using ReturnRight.Api.Extraction;
using ReturnRight.Api.Features.Auth;
using ReturnRight.Api.Features.Cases;
using ReturnRight.Api.Features.Cases.NextAction;
using ReturnRight.Api.Features.Cases.Readiness;
using ReturnRight.Api.Features.Documents;
using ReturnRight.Api.Features.Evidence;
using ReturnRight.Api.Features.Exports;
using ReturnRight.Api.Features.FollowUps;
using ReturnRight.Api.Features.Intakes;
using ReturnRight.Api.Features.Interactions;
using ReturnRight.Api.Features.Notifications;
using ReturnRight.Api.Features.Profile;
using ReturnRight.Api.Features.Purchases;
using ReturnRight.Api.Features.Reminders;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Security;
using ReturnRight.Api.Storage;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 11 * 1024 * 1024;
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.AddReturnRightPersistence();
builder.AddReturnRightAuth();
builder.Services.AddReturnRightRateLimiting();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CurrentUser>();

builder.Services.AddReturnRightStorage();
builder.Services.AddReturnRightExtraction(builder.Configuration);
builder.Services.AddReturnRightEmail();
builder.Services.AddReturnRightReminders(builder.Configuration);
builder.Services.AddReturnRightIntakes(builder.Configuration);

builder.Services.AddScoped<CaseDetailBuilder>();
builder.Services.AddScoped<CaseTimelineWriter>();
builder.Services.AddScoped<CaseFileGenerator>();

var app = builder.Build();

await app.Services.ApplyStartupTasksAsync(app.Environment, app.Configuration);

// Unhandled exceptions become a consumer-friendly problem+json — never a stack trace.
app.UseExceptionHandler(errorApp =>
    errorApp.Run(context => ProblemResults.WriteAsync(
        context.Response,
        StatusCodes.Status500InternalServerError,
        "Something went wrong on our side. Please try again.")));
app.UseStatusCodePages();
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<AntiforgeryMiddleware>();

app.MapGet("/api/ping", () => Results.Ok(new { pong = true }));
app.MapGet("/health", () => Results.Json(new { status = "ok" }));

app.MapGroup("/api/auth").MapAuthEndpoints();
app.MapGroup("/api/profile").RequireAuthorization().MapProfileEndpoints();
app.MapGroup("/api/purchases").RequireAuthorization().MapPurchaseEndpoints();
app.MapGroup("/api/documents").RequireAuthorization().MapDocumentEndpoints();
app.MapGroup("/api/intakes").RequireAuthorization().MapIntakeEndpoints();
app.MapGroup("/api/notifications").RequireAuthorization().MapNotificationEndpoints();
app.MapHomeEndpoints();
var casesGroup = app.MapGroup("/api/cases").RequireAuthorization();
casesGroup.MapCaseEndpoints();
casesGroup.MapEvidenceEndpoints();
casesGroup.MapInteractionEndpoints();
casesGroup.MapFollowUpEndpoints();
casesGroup.MapReminderEndpoints();
casesGroup.MapReadinessEndpoints();
casesGroup.MapNextActionEndpoints();
casesGroup.MapCaseFileEndpoints();

if (app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Dev:EnableTestEndpoints", false))
{
    app.MapGroup("/api/dev").RequireAuthorization().MapDevReminderEndpoints();
}

app.Run();

public partial class Program;
