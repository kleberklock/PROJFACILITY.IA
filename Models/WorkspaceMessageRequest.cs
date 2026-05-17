using System;

namespace PROJFACILITY.IA.Models
{
    public class WorkspaceMessageRequest
    {
        public Guid SessionId { get; set; }
        public string UserMessage { get; set; } = string.Empty;
    }
}
