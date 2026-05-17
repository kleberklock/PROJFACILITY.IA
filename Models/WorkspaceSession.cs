using System;
using System.ComponentModel.DataAnnotations;

namespace PROJFACILITY.IA.Models
{
    public class WorkspaceSession
    {
        [Key]
        public Guid Id { get; set; }
        
        public int AdminUserId { get; set; }
        
        public int AgentId { get; set; }
        
        public DateTime StartTime { get; set; }
        
        public DateTime LastActivity { get; set; }
        
        public string ActiveContext { get; set; } = string.Empty;
    }
}
