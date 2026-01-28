using System;
using System.ComponentModel.DataAnnotations;

namespace PROJFACILITY.IA.Models
{
    public class KnowledgeDocument
    {
        [Key]
        public int Id { get; set; }
        
        public int UserId { get; set; }
        public string FileName { get; set; } = string.Empty;
        
        // --- NOVAS PROPRIEDADES ---
        public string FileType { get; set; } = string.Empty;
        public string VectorIdPrefix { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty; // Profissão
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}