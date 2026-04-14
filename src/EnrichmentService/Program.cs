using DocIntelligence.Contracts;
using EnrichmentService.Consumers;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());

builder.Services.AddMassTransit(x =>
{
    var messageLimit = builder.Configuration.GetValue<int>("MessageLimit");
    var interval = builder.Configuration.GetValue<int>("Interval");

    x.AddConfigureEndpointsCallback((_, cfg) =>
    {
        if (cfg is IRabbitMqReceiveEndpointConfigurator endpointConfigurator)
            endpointConfigurator.SingleActiveConsumer = true;
    });
    
    x.AddConsumer<DocumentSubmittedConsumer>(cfg =>
    {
        cfg.Options<BatchOptions>(options =>
            options
                .SetMessageLimit(messageLimit)
                .SetTimeLimit(s: interval)
                .SetTimeLimitStart(BatchTimeLimitStart.FromLast)
        );
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.PrefetchCount = messageLimit;
        cfg.ConcurrentMessageLimit = messageLimit;

        cfg.Host(builder.Configuration["RabbitMq:Host"] ?? "localhost", "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("document-submitted", e =>
        {
            e.UseRateLimit(messageLimit, TimeSpan.FromSeconds(interval));
            e.UseMessageRetry(r => { r.Interval(5, TimeSpan.FromSeconds(1)); });
            e.ConfigureConsumer<DocumentSubmittedConsumer>(context);
        });
    });
});

var app = builder.Build();

app.Run("http://0.0.0.0:5060");