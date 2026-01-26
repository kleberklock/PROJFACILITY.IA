using System;
using System.ComponentModel.DataAnnotations;

namespace PROJFACILITY.IA.Models
{
    public class Agent
    {
        [Key]
        public int Id { get; set; }
        
        // NOVO: Dono do Agente. Se NULL, é um agente do sistema (público).
        public int? UserId { get; set; } 

        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty; // Mantido conforme seu uso no Controller
        public string SystemInstruction { get; set; } = string.Empty;
        
        // Opcional: Para facilitar filtros
        public bool IsPublic { get; set; } = false; 
    }
}