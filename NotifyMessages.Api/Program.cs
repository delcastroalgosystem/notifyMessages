using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using NotifyMessages.Api.Authentication;
using NotifyMessages.Api.Middleware;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Application.Services;
using NotifyMessages.Application.Validators;
using NotifyMessages.Infrastructure.Persistence;
using NotifyMessages.Infrastructure.Providers;
using NotifyMessages.Infrastructure.Providers.Brevo;
using NotifyMessages.Infrastructure.Providers.Egoi;
using NotifyMessages.Infrastructure.Providers.SendGrid;
using NotifyMessages.Infrastructure.Providers.Twilio;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, AppDbContext.ConfigureSqlServer));

builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
builder.Services.AddScoped<IDispatchService, DispatchService>();
builder.Services.AddScoped<IWebhookEventService, WebhookEventService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<ITriggerService, TriggerService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<IDispatchQueryService, DispatchQueryService>();
builder.Services.AddScoped<ISuppressionService, SuppressionService>();
builder.Services.AddScoped<ITenantAdminService, TenantAdminService>();
builder.Services.AddSingleton<IBatchFileParser, NotifyMessages.Infrastructure.Import.BatchFileParser>();
builder.Services.AddValidatorsFromAssemblyContaining<DispatchRequestDtoValidator>();

builder.Services.AddHttpClient("Egoi");
builder.Services.AddHttpClient("EgoiCampaign");
builder.Services.AddHttpClient("Brevo");

builder.Services.AddSingleton<IEmailProvider, EgoiEmailProvider>();
builder.Services.AddSingleton<ISmsProvider, EgoiSmsProvider>();
builder.Services.AddSingleton<ICampaignProvider, EgoiCampaignProvider>();
builder.Services.AddSingleton<IEmailProvider, SendGridEmailProvider>();
builder.Services.AddSingleton<ISmsProvider, TwilioSmsProvider>();
builder.Services.AddSingleton<IEmailProvider, BrevoEmailProvider>();
builder.Services.AddSingleton<ISmsProvider, BrevoSmsProvider>();
builder.Services.AddScoped<IProviderFactory, ProviderFactory>();

builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.SchemeName, null)
    // Só usado nos controllers /api/v1/admin (AdminControllerBase); a X-Api-Key continua a ser o esquema por omissão.
    .AddScheme<AdminKeyAuthenticationOptions, AdminKeyAuthenticationHandler>(AdminKeyAuthenticationOptions.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(ApiKeyAuthenticationOptions.SchemeName, new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = ApiKeyAuthenticationOptions.HeaderName,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "API Key do Tenant, enviada no header X-Api-Key."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = ApiKeyAuthenticationOptions.SchemeName
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Opt-in: numa BD partilhada (SMA_EXTRATOOLS) não se querem tenants/templates de exemplo
    if (app.Configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbSeeder.SeedAsync(dbContext, app.Logger);
    }
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();