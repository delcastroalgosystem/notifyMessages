using Microsoft.EntityFrameworkCore;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Application.Options;
using NotifyMessages.Infrastructure.Persistence;
using NotifyMessages.Infrastructure.Providers;
using NotifyMessages.Infrastructure.Providers.Brevo;
using NotifyMessages.Infrastructure.Providers.Egoi;
using NotifyMessages.Infrastructure.Providers.SendGrid;
using NotifyMessages.Infrastructure.Providers.Twilio;
using NotifyMessages.Worker;

var builder = Host.CreateApplicationBuilder(args);
// Como serviço Windows (sc.exe): avisa o SCM do arranque/paragem e regista no Visualizador de Eventos
// (origem NotifyMessages.Worker, criada na instalação). Na consola não muda nada.
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "NotifyMessagesWorker";
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, AppDbContext.ConfigureSqlServer));

builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
// Seguro por omissão: fora de Produção, nada chega a um destinatário real sem se configurar isso explicitamente.
builder.Services.PostConfigure<WorkerOptions>(o => o.ForceSandbox ??= !builder.Environment.IsProduction());

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

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
