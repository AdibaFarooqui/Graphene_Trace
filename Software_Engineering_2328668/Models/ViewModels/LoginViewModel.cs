using System.ComponentModel.DataAnnotations;

namespace Software_Engineering_2328668.Models.ViewModels
{
    public class LoginViewModel
    {
    
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }
}
