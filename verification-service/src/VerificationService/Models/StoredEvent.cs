namespace VerificationService.Models;

public class EventRecord
{
    public string StreamId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal? Amount { get; set; }
    //public string? DebitAccount { get; set; }
    //public string? CreditAccount { get; set; }
    //public DateTime? OccurredAt { get; set; }
}