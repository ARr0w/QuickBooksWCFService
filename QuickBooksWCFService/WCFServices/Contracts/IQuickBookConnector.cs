using CoreWCF;
using System.Xml;
using System.Xml.Serialization;

namespace QuickBooksWCFService.WCFServices.Contracts
{
    [ServiceContract(Namespace = "http://developer.intuit.com/")]
    public interface IQuickBookConnector
    {
        [OperationContract(Action = "http://developer.intuit.com/clientVersion")]
        string clientVersion(string strVersion);

        [OperationContract(Action = "http://developer.intuit.com/serverVersion")]
        string serverVersion();

        [OperationContract(Action = "http://developer.intuit.com/authenticate")]
        [XmlSerializerFormat]
        [return: XmlArray("authenticateResult"), XmlArrayItem("string")]
        string[] authenticate(string strUserName, string strPassword);

        [OperationContract(Action = "http://developer.intuit.com/sendRequestXML")]
        [XmlSerializerFormat]
        [return: XmlElement("sendRequestXMLResult")]
        string sendRequestXML(string ticket, string strHCPResponse, string strCompanyFileName, string strCountry, int qbXMLMajorVers, int qbXMLMinorVers);

        [OperationContract(Action = "http://developer.intuit.com/receiveResponseXML")]
        int receiveResponseXML(string ticket, string response, string hresult, string message);

        [OperationContract(Action = "http://developer.intuit.com/connectionError")]
        string connectionError(string ticket, string hresult, string message);

        [OperationContract(Action = "http://developer.intuit.com/getLastError")]
        string getLastError(string ticket);

        [OperationContract(Action = "http://developer.intuit.com/closeConnection")]
        string closeConnection(string ticket);
    }
}
