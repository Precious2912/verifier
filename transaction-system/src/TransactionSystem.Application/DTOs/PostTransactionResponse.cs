namespace TransactionSystem.Application.DTOs;

public class PostTransactionResponse
{
    public string Reference { get; set; } = string.Empty;
    public string DebitAccount { get; set; } = string.Empty;
    public string CreditAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}