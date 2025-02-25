using System.Xml;
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

        private readonly Dictionary<string, string> _requests;

        public QuickBookConnector(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<QuickBookConnector> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _scopeFactory = scopeFactory;

            _requests = BuildRequest();
        }

        public string ClientVersion(string strVersion) => "1.0";

        public string ServerVersion() => "QBWC WCF Service v1.0";

        public string[] Authenticate(string strUserName, string strPassword)
        {
            _logger.LogInformation("Athenticating");

            var creds = _configuration["Secret"]?.Split("||");

            string[] authReturn = new string[2];
            authReturn[0] = Guid.NewGuid().ToString();

            if (creds != null && strUserName.IsHashValid(creds[1]) && strPassword.IsHashValid(creds[0]))
            {
                _sessionDetails.Add(authReturn[0], new Dictionary<string, object>());
                _logger.LogInformation("User Authenticated");
                _sessionDetails[authReturn[0]].Add("userName", strUserName);
                authReturn[1] = string.Empty;
                return authReturn; // Empty means use the default QB Company file
            }

            _logger.LogInformation("Invalid User");
            authReturn[1] = "nvu";

            return authReturn;
        }

        public XmlElement? SendRequestXML(string ticket, string strHCPResponse, string strCompanyFileName, string strCountry, int qbXMLMajorVers, int qbXMLMinorVers)
        {
            _logger.LogInformation("Sending request XML - ItemInventoryQueryRq.xml");

            XmlDocument doc = new XmlDocument();

            if (!_sessionDetails.ContainsKey(ticket))
            {
                return doc.DocumentElement;
            }

            if (!_sessionDetails[ticket].ContainsKey("counter"))
            {
                _sessionDetails[ticket].Add("counter", 0);
            }

            int count = Convert.ToInt32(_sessionDetails[ticket]["counter"]);

            string request = "";

            if (count < _requests.Count)
            {
                request = _requests.ElementAt(count).Value;
                doc.LoadXml(request);
                _sessionDetails[ticket]["counter"] = count + 1;
            }
            else
            {
                _sessionDetails[ticket]["counter"] = 0;
            }

            return doc.DocumentElement;
        }

        public int ReceiveResponseXML(string ticket, string response, string hresult, string message)
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

                var responseOf = _requests.ElementAt((Convert.ToInt32(_sessionDetails[ticket]["counter"]) - 1));

                var serviceFactory = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ServiceFactory>();
                var service = serviceFactory.GetService(responseOf.Key);
                if (service != null)
                {
                    _ = Task.Run(() => service.HandleDataAsync(response));
                }

                int total = _requests.Count;
                var counter = Convert.ToInt32(_sessionDetails[ticket]["counter"]);
                int percentage = (counter * 100) / total;
                retVal = percentage >= 100 ? 0 : percentage;
            }

            logMessages.Add($"Return values:");
            logMessages.Add($"int retVal = {retVal}");

            _logger.LogInformation(string.Join(Environment.NewLine, logMessages));

            return retVal;
        }

        public string ConnectionError(string ticket, string hresult, string message)
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

        public string GetLastError(string ticket)
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

        public string CloseConnection(string ticket)
        {
            _sessionDetails.Remove(ticket);
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