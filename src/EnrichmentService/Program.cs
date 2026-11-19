using System.Net;
using EnrichmentService.Services;
using EnrichmentService.Consumers;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AIRateLimiter>();
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
            e.ConcurrentMessageLimit = 30;

            e.UseMessageRetry(r =>
            {
                r.Exponential(
                    3, 
                    TimeSpan.FromSeconds(1), 
                    TimeSpan.FromSeconds(10), 
                    TimeSpan.FromSeconds(2));
                
                r.Handle<HttpRequestException>(ex =>
                    ex.StatusCode is
                        HttpStatusCode.TooManyRequests or
                        HttpStatusCode.ServiceUnavailable or
                        HttpStatusCode.BadGateway or
                        HttpStatusCode.GatewayTimeout);
            });

            e.ConfigureConsumer<DocumentSubmittedConsumer>(context);
        });
    });
});

var app = builder.Build();

app.Run("http://0.0.0.0:5060");
