using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models;

[Table("QuestionPurgeAudit")]
public sealed class QuestionPurgeAudit
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long QuestionPurgeAuditId { get; set; }

    // Deliberately not a foreign key: the source Question is removed after purge.
    public int QuestionId { get; set; }
    public int? RequestedBy { get; set; }
    public DateTime? SoftDeletedAt { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime PurgedAt { get; set; }

    [MaxLength(40)]
    public string Outcome { get; set; } = "Purged";

    [MaxLength(1000)]
    public string? Detail { get; set; }
}
