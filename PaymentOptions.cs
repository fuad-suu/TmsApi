using System.ComponentModel.DataAnnotations;

public class PaymentOptions
{
    // The [Required] attribute ensures this cannot be null or empty
    [Required(ErrorMessage = "The GatewayUrl field is required.")]
    public required string GatewayUrl { get; init; }

    // The [Range] attribute ensures the deposit amount falls within legal limits
    [Range(100, 100000, ErrorMessage = "Deposit must be between 100 and 100,000 Birr.")]
    public decimal MaxDepositBirr { get; init; }
}