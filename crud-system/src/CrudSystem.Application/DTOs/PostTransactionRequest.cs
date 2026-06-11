using System.ComponentModel.DataAnnotations;

namespace CrudSystem.Application.DTOs;

public class PostTransactionRequest
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(30)]
    public required string DebitAccount { get; set; }

    [Required]
    [MaxLength(30)]
    public required string CreditAccount { get; set; }
}