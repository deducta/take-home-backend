using MassTransit;
using Microsoft.EntityFrameworkCore;
using PersistenceService.Consumers;
using PersistenceService.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? "Host=localhost;Database=docintelligence;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddMassTransit(x =>
{
    var messageLimit = builder.Configuration.GetValue<int>("MessageLimit");
    x.AddConsumer<DocumentEnrichedConsumer>(cfg =>
    {
        cfg.Options<BatchOptions>(options => options.SetMessageLimit(messageLimit));
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.PrefetchCount = messageLimit;
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("document-enriched", e =>
        {
            e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));
            e.ConfigureConsumer<DocumentEnrichedConsumer>(context);
        });
    });
});

var app = builder.Build();

// Auto-create schema on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.Run("http://0.0.0.0:5070");