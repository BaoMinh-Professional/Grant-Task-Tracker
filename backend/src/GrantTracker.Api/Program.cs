using System.Text.Json.Serialization;
using GrantTracker.Api.Configuration;
using GrantTracker.Api.DependencyInjection;
using GrantTracker.Api.Errors;
using GrantTracker.Api.Identity;
using GrantTracker.Api.Logging;
using GrantTracker.Core.Common.Time;
using GrantTracker.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog((services, configuration) => SerilogConfiguration.Configure(services, configuration));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsSetup.Customize);
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ProblemDetailsSetup.InvalidModelState);
var cors = builder.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(cors.AllowedOrigins)
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
    .WithHeaders(HeaderNames.UserId, HeaderNames.IdempotencyKey, "Content-Type")));
builder.Services.AddOptions<BusinessTimeOptions>()
    .Bind(builder.Configuration.GetSection(BusinessTimeOptions.SectionName))
    .Validate(options => { try { _ = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId); return true; } catch { return false; } },
        "BusinessTime:TimeZoneId must identify an installed time zone.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBusinessClock>(services =>
{
    var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BusinessTimeOptions>>().Value;
    return new BusinessClock(services.GetRequiredService<TimeProvider>(), TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId));
});
builder.Services.AddScoped<HttpCurrentUser>();
builder.Services.AddScoped<GrantTracker.Core.Common.Identity.ICurrentUser>(services => services.GetRequiredService<HttpCurrentUser>());
builder.Services.AddGrantTrackerCore();
builder.Services.AddGrantTrackerData(ConnectionStrings.Resolve);

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitialiseAsync();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseRouting();
app.UseCors();
app.UseMiddleware<CurrentUserMiddleware>();
app.MapControllers();
app.Run();

public partial class Program;
