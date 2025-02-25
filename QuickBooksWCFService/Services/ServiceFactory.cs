using QuickBooksWCFService.Services.Contract;

namespace QuickBooksWCFService.Services
{
    public class ServiceFactory
    {
        private readonly Dictionary<string, Func<IParseService>> _services;
        private readonly ILogger<ServiceFactory> _logger;
        private readonly IConfiguration _configuration;

        public ServiceFactory(IConfiguration configuration, ILogger<ServiceFactory> logger)
        {
            _logger = logger;
            _configuration = configuration;

            _services = new Dictionary<string, Func<IParseService>>()
            {
                { "ItemInventoryQueryRq", () => new InventoryService(_configuration, _logger) }
            };
        }

        public IParseService? GetService(string requestQbXmlFile)
        {
            if(_services.TryGetValue(requestQbXmlFile, out var serviceInitialzer))
            {
                return serviceInitialzer();
            }

            return null;
        }
    }
}
