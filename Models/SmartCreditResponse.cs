
namespace JunkiesSoftware.Models;

public class SmartCreditResponse
{
    public string AccountStatus { get; set; }
    public string AccountStatusCause { get; set; }
    public Boolean IsClosePending { get; set; }
    public DateTime CloseDate { get; set; }
}