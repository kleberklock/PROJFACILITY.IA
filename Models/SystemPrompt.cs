using System.ComponentModel.DataAnnotations;

namespace PROJFACILITY.IA.Models
{
    public class SystemPrompt
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Area { get; set; } // Ex: Jurídico, Tech

        [Required]
        public string Profession { get; set; } // Ex: Advogado Civil

        [Required]
        public string ButtonTitle { get; set; } // Título do Botão

        [Required]
        public string Content { get; set; } // O prompt oculto (conteúdo)
    }
}