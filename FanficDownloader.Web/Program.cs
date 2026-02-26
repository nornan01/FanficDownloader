using FanficDownloader.Core.Sources;
using FanficDownloader.Application.Services;
using System.Text;


Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddRazorPages();


// Регистрируем зависимости
builder.Services.AddSingleton<SourceManager>();
builder.Services.AddScoped<FanficDownloadService>();
builder.Services.AddHttpClient<ImageDownloadService>();
builder.Services.AddSingleton<DownloadQueueService>();


var app = builder.Build();
var queue = app.Services.GetRequiredService<DownloadQueueService>();
await queue.StartWorkers(3);   // одновременно 3 загрузки

app.UseSwagger();
app.UseSwaggerUI();


// подключаем контроллеры
app.MapControllers();
app.UseStaticFiles();
app.MapRazorPages();

app.Run();
