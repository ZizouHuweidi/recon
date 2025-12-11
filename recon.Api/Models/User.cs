using System.ComponentModel.DataAnnotations;

namespace recon.Api.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    public string? Role { get; set; }
}
