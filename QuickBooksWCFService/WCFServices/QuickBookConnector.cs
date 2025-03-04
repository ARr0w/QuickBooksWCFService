using QuickBooksWCFService.Extensions;
using QuickBooksWCFService.Services;
using QuickBooksWCFService.WCFServices.Contracts;

namespace QuickBooksWCFService.WCFServices
{
    public class QuickBookConnector : IQuickBookConnector
    {
        private static readonly Dictionary<string, Dictionary<string, object>> _sessionDetails = new();

        private readonly IConfiguration _configuration;
        private readonly ILogger<QuickBookConnector> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        private static readonly Dictionary<string, string> QbErrorMessages = new()
        {
            { "0x80040400", "QuickBooks found an error when parsing the provided XML text stream." },
            { "0x80040401", "Could not access QuickBooks." },
            { "0x80040402", "Unexpected error. Check the qbsdklog.txt file for additional information." }
        };

        public QuickBookConnector(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<QuickBookConnector> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public string clientVersion(string strVersion)
        {
            _logger.LogInformation($"Received Quickbook version {strVersion}");

            return "O:1.0";
        }

        public string serverVersion() => "QBWC WCF Service v1.0";

        public string[] authenticate(string strUserName, string strPassword)
        {
            _logger.LogInformation("Authenticating user: {UserName}", strUserName);

            string[] authReturn = new string[2];
            authReturn[0] = Guid.NewGuid().ToString();
            authReturn[1] = "nvu";

            var secret = _configuration["Secret"];
            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogError("Secret is not configured.");
                return authReturn;
            }

            var creds = secret.Split("||", StringSplitOptions.RemoveEmptyEntries);
            if (creds.Length < 2)
            {
                _logger.LogError("Secret is not properly formatted. Expected format: 'password||username'.");
                return authReturn;
            }

            if (strUserName.IsHashValid(creds[1]) && strPassword.IsHashValid(creds[0]))
            {
                _sessionDetails.Add(authReturn[0], new Dictionary<string, object>());
                _logger.LogInformation("User Authenticated. Ticket: {Ticket}", authReturn[0]);
                _sessionDetails[authReturn[0]].Add("userName", strUserName);

                authReturn[1] = _configuration["companyFilePath"] ?? "";
            }
            else
            {
                _logger.LogInformation("Invalid User. Ticket: {Ticket}", authReturn[0]);
            }

            return authReturn;
        }

        public string sendRequestXML(string ticket, string strHCPResponse, string strCompanyFileName, string strCountry, int qbXMLMajorVers, int qbXMLMinorVers)
        {
            _logger.LogInformation("Sending request XML - ItemInventoryQueryRq.xml");

            if (!_sessionDetails.ContainsKey(ticket))
            {
                return "";
            }

            if (!_sessionDetails[ticket].ContainsKey("counter"))
            {
                _sessionDetails[ticket].Add("counter", 0);
            }

            int count = Convert.ToInt32(_sessionDetails[ticket]["counter"]);

            Dictionary<string, string> req = BuildRequest();
            string request = "";

            if (count < req.Count)
            {
                request = req.ElementAt(count).Value;
                _sessionDetails[ticket]["counter"] = count + 1;
            }
            else
            {
                _sessionDetails[ticket]["counter"] = 0;
            }

            return request;
        }

        public int receiveResponseXML(string ticket, string response, string hresult, string message)
        {
            if (!_sessionDetails.ContainsKey(ticket))
            {
                return 0;
            }

            var logMessages = new List<string>
            {
                "WebMethod: receiveResponseXML() has been called by QBWebconnector",
                "",
                "Parameters received:",
                $"string ticket = {ticket}",
                $"string hresult = {hresult}",
                $"string message = {message}",
                ""
            };

            int retVal = 0;

            if (!string.IsNullOrEmpty(hresult))
            {
                logMessages.Add($"HRESULT = {hresult}");
                logMessages.Add($"Message = {message}");
                retVal = -101; // Error case
            }
            else
            {
                logMessages.Add($"Length of response received = {response.Length}");

                var requests = BuildRequest();
                var responseOf = requests.ElementAt((Convert.ToInt32(_sessionDetails[ticket]["counter"]) - 1));

                var serviceFactory = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ServiceFactory>();
                var service = serviceFactory.GetService(responseOf.Key);
                if (service != null)
                {
                    _ = Task.Run(() => service.HandleDataAsync(response));
                }

                int total = requests.Count;
                var counter = Convert.ToInt32(_sessionDetails[ticket]["counter"]);
                int percentage = (counter * 100) / total;
                retVal = percentage >= 100 ? 100 : percentage;
            }

            logMessages.Add($"Return values:");
            logMessages.Add($"int retVal = {retVal}");

            _logger.LogInformation(string.Join(Environment.NewLine, logMessages));

            return retVal;
        }

        public string connectionError(string ticket, string hresult, string message)
        {
            var logMessages = new List<string>
            {
                "Parameters received:",
                $"string ticket = {ticket}",
                $"string hresult = {hresult}",
                $"string message = {message}"
            };

            string retVal = "DONE";

            if (QbErrorMessages.TryGetValue(hresult.Trim(), out var errorMsg))
            {
                logMessages.Add($"HRESULT = {hresult}");
                logMessages.Add($"Message = {errorMsg}");
            }
            else
            {
                logMessages.Add($"HRESULT = {hresult}");
                logMessages.Add($"Message = {message}");
                logMessages.Add("Sending DONE to stop.");
            }

            logMessages.Add($"Return values: string retVal = {retVal}");
            _logger.LogInformation(string.Join(Environment.NewLine, logMessages));

            return retVal;
        }

        public string getLastError(string ticket)
        {
            string evLogTxt = $"WebMethod: GetLastError() has been called by QBWebconnector\r\n\r\n" +
                   $"Parameters received:\r\n" +
                   $"string ticket = {ticket}\r\n\r\n";

            int errorCode = 0;
            string retVal = (errorCode == -101) ? "QuickBooks was not running!" : "Error!";

            evLogTxt += $"\r\nReturn values: \r\nstring retVal = {retVal}\r\n";

            _logger.LogError(evLogTxt);
            return retVal;
        }

        public string closeConnection(string ticket)
        {
            _sessionDetails.Remove(ticket);
            _logger.LogInformation("Close Collection Called. Ticket {Ticket} has been removed.", ticket);
            return "OK";
        }

        private Dictionary<string, string> BuildRequest()
        {
            _logger.LogInformation("Preparing XML requests...");

            var requestXmlFileNames = _configuration.GetSection("RequestXml").Get<List<string>>();

            if (requestXmlFileNames == null || requestXmlFileNames.Count == 0)
            {
                _logger.LogWarning("No request XML files are specified in the configuration.");
                return new Dictionary<string, string>();
            }

            var xmlRequests = new Dictionary<string, string>();
            string xmlFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RequestQbXmlFiles");

            foreach (var requestXmlFileName in requestXmlFileNames)
            {
                var xmlFilePath = Path.Combine(xmlFolderPath, requestXmlFileName);

                if (!File.Exists(xmlFilePath))
                {
                    _logger.LogWarning("Skipping '{FileName}'. File not found in directory: {FolderPath}", requestXmlFileName, xmlFolderPath);
                    continue;
                }

                _logger.LogInformation("Loading XML request from file: {FileName}", requestXmlFileName);
                string xmlRequest = File.ReadAllText(xmlFilePath);
                xmlRequests.Add(Path.GetFileNameWithoutExtension(requestXmlFileName), xmlRequest);
            }

            _logger.LogInformation("Successfully prepared {Count} XML request(s).", xmlRequests.Count);
            return xmlRequests;
        }
    }
}