using System.ComponentModel.DataAnnotations;

namespace SxgEvalPlatformApi.Models.Dtos
{
    /// <summary>
    /// Data transfer object for user metadata information
    /// </summary>
    public class UserMetadataDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(255, MinimumLength = 1)]
        public string Email { get; set; } = string.Empty;
    }
}