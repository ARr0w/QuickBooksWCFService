using System.Xml.Linq;
using QuickBooksWCFService.Services.Contract;

namespace QuickBooksWCFService.Services
{
    public class InventoryService(IConfiguration configuration, ILogger logger) : IParseService
    {
        private readonly ILogger _logger = logger;
        private readonly IConfiguration _configuration = configuration;

        private string _response = string.Empty;

        public async Task HandleDataAsync(string response)
        {
            _response = response.Trim();
            _logger.LogInformation("Received response. Starting parsing process.");

            try
            {
                XDocument doc = XDocument.Parse(_response);
                _logger.LogInformation("Successfully parsed XML response.");

                var limit = int.Parse(_configuration["parseLimit"]!);

                foreach (var item in doc.Descendants("ItemInventoryRet"))
                {
                    // do work here
                    await Task.FromResult("");
                }

                _logger.LogInformation("Finished processing response.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while handling data.");
            }
        }
    }
}

