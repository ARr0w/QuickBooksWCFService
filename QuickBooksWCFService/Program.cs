using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using QuickBooksWCFService.Services;
using QuickBooksWCFService.WCFServices;
using QuickBooksWCFService.WCFServices.Contracts;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog for logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/qbConnector-log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Register Controllers & OpenAPI
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add CoreWCF Services
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();

// Enable detailed WCF exceptions for debugging
builder.Services.AddSingleton<IServiceBehavior>(new ServiceDebugBehavior
{
    IncludeExceptionDetailInFaults = true
});

// Register dependencies
builder.Services.AddScoped<ServiceFactory>();
builder.Services.AddSingleton<QuickBookConnector>();

var app = builder.Build();

// Configure CoreWCF service endpoints
app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<QuickBookConnector>(service =>
    {
        service.DebugBehavior.IncludeExceptionDetailInFaults = true;
    });

    serviceBuilder.AddServiceEndpoint<QuickBookConnector, IQuickBookConnector>(
        new BasicHttpBinding(BasicHttpSecurityMode.None),  // Ensure HTTP binding (matching QuickBooks expectations)
        "/QuickBookConnector"
    );

    // Enable WSDL metadata publishing
    var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    serviceMetadataBehavior.HttpGetEnabled = true;
    serviceMetadataBehavior.HttpsGetEnabled = true;
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Middleware Pipeline
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Ensure metadata service is registered
var metadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
metadataBehavior.HttpGetEnabled = true;
metadataBehavior.HttpsGetEnabled = true;

app.Run();