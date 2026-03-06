using FanficDownloader.Web;
using FanficDownloader.Application.Configuration;
using System.Text;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddRazorPages();

builder.Services.Configure<DownloadSettings>(builder.Configuration.GetSection("Download"));
builder.Services.Configure<FlareSolverrSettings>(builder.Configuration.GetSection("FlareSolverr"));

builder.Services.AddFanficDownloader();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.UseStaticFiles();
app.MapRazorPages();

app.MapGet("/health", () => Results.Ok("OK"));

app.Run();