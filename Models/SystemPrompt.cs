using System.ComponentModel.DataAnnotations;

namespace PROJFACILITY.IA.Models
{
    public class SystemPrompt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public required string Area { get; set; } // Adicionado 'required'

        [Required]
        public required string Profession { get; set; } // Adicionado 'required'

        [Required]
        public required string ButtonTitle { get; set; } // Adicionado 'required'

        [Required]
        public required string Content { get; set; } // Adicionado 'required'
    }
}