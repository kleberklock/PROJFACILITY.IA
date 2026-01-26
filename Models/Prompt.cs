using System;
using System.ComponentModel.DataAnnotations;

namespace PROJFACILITY.IA.Models
{
    public class Prompt
    {
        [Key]
        public int Id { get; set; }
        
        public int UserId { get; set; } // Dono do prompt
        
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsFavorite { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}