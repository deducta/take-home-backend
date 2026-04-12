using EnrichmentService.Consumers;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<DocumentSubmittedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("document-submitted", e =>
        {
            e.PrefetchCount = 50;
            e.ConcurrentMessageLimit = 20;

            e.UseMessageRetry(r =>
            {
                r.Interval(5, TimeSpan.FromSeconds(1));
            });

            e.ConfigureConsumer<DocumentSubmittedConsumer>(context);
        });
    });
});

var app = builder.Build();

app.Run("http://0.0.0.0:5060");
