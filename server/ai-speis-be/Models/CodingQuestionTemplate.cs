using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ai_speis_be.Models
{
    [Table("CodingQuestionTemplate")]
    public class CodingQuestionTemplate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TemplateId { get; set; }

        [Required]
        [ForeignKey("CodingQuestion")]
        public int CodingQuestionId { get; set; }

        [Required]
        public int LanguageId { get; set; } // Judge0 Language ID

        [Required]
        public string TemplateCode { get; set; } = null!;

        // Navigation properties
        public virtual CodingQuestion CodingQuestion { get; set; } = null!;
    }
}
