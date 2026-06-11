using System.ComponentModel.DataAnnotations;

namespace CrudSystem.Application.DTOs;

public class CreateAccountRequest
{
    [Required]
    [MaxLength(100)]
    public string AccountName { get; set; } = string.Empty;
}