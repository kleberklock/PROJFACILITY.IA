using System;
using System.ComponentModel.DataAnnotations;

namespace PROJFACILITY.IA.Models
{
    public class KnowledgeDocument
    {
        [Key]
        public int Id { get; set; }
        
        // NOVO: Vincula o documento a um usuário específico
        public int UserId { get; set; } 
        
        public string FileName { get; set; } = string.Empty;
        public string AgentName { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}