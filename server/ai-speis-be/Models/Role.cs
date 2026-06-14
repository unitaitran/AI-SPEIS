using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
namespace ai_speis_be.Models
{
    [Table("Role")]
    [Index(nameof(RoleId), Name = "IX_Role_RoleId", IsUnique = true)]
    [Index(nameof(Status), Name = "IX_Role_Status")]
    public class Role
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoleId { get; set; }
        [Required]
        [MaxLength(100)]
        public string RoleName { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Description { get; set; }

        public bool Status { get; set; } = true;

        
    }
}

