namespace QuickBooksWCFService.Services.Contract
{
    public interface IParseService
    {
        Task HandleDataAsync(string response);
    }
}
