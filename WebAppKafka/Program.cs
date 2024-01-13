using WebAppKafka.Services;
using WebAppKafka.Settings;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register KafkaConfiguration
builder.Services.Configure<KafkaConfiguration>(builder.Configuration.GetSection(nameof(KafkaConfiguration)));

// Register KafkaConsumer as BackgroundService
builder.Services.AddSingleton<KafkaBackgroundService>();
builder.Services.AddSingleton<IHostedService>(p => p.GetService<KafkaBackgroundService>());

// register KafkaProducer
builder.Services.AddTransient<IKafkaProducer, KafkaProducer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
