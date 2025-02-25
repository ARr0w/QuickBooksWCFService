using CoreWCF;
using System.Xml;

namespace QuickBooksWCFService.WCFServices.Contracts
{
    [ServiceContract(Namespace = "http://tempuri.org/")]
    public interface IQuickBookConnector
    {
        [OperationContract(Action = "http://tempuri.org/ClientVersion")]
        string ClientVersion(string strVersion);

        [OperationContract(Action = "http://tempuri.org/ServerVersion")]
        string ServerVersion();

        [OperationContract(Action = "http://tempuri.org/Authenticate")]
        string[] Authenticate(string strUserName, string strPassword);

        [OperationContract(Action = "http://tempuri.org/SendRequestXML")]
        XmlElement? SendRequestXML(string ticket, string strHCPResponse, string strCompanyFileName, string strCountry, int qbXMLMajorVers, int qbXMLMinorVers);

        [OperationContract(Action = "http://tempuri.org/ReceiveResponseXML")]
        int ReceiveResponseXML(string ticket, string response, string hresult, string message);

        [OperationContract(Action = "http://tempuri.org/ConnectionError")]
        string ConnectionError(string ticket, string hresult, string message);

        [OperationContract(Action = "http://tempuri.org/GetLastError")]
        string GetLastError(string ticket);

        [OperationContract(Action = "http://tempuri.org/CloseConnection")]
        string CloseConnection(string ticket);
    }
}
