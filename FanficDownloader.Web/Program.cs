using FanficDownloader.Core.Sources;
using FanficDownloader.Application.Services;
using FanficDownloader.Core.Parsers;
using FanficDownloader.Core.Formatting;
using FanficDownloader.Core.Clients;
using FanficDownloader.Bot.Services;
using FanficDownloader.Web.Services;


using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

builder.Services.AddHostedService<TelegramBotBackgroundService>();
builder.Services.AddSingleton<DownloadQueueService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DownloadQueueService>());
builder.Services.AddHttpClient<FanficEpubFormatter>();



builder.Services.AddSingleton<SourceManager>();
builder.Services.AddScoped<FanficDownloadService>();
builder.Services.AddHttpClient<ImageDownloadService>();
builder.Services.AddScoped<FanficService>();

// Парсеры
builder.Services.AddTransient<FicbookParser>();
builder.Services.AddTransient<SnapetalesParser>();
builder.Services.AddTransient<FanfictionNetParser>();
builder.Services.AddTransient<WalkingThePlankParser>();

// FlareSolverr
builder.Services.AddHttpClient<FlareSolverrClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8191");
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddHttpClient<IFanficSource, FicbookSource>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IFanficSource, SnapetalesSource>();
builder.Services.AddTransient<IFanficSource, FanfictionNetSource>();
builder.Services.AddHttpClient<IFanficSource, WalkingThePlankSource>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.UseStaticFiles();
app.MapRazorPages();

app.Run();