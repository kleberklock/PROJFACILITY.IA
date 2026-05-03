namespace PROJFACILITY.IA.Models
{
    public class EmailSettingsOptions
    {
        public string Remetente { get; set; } = string.Empty;
        public string SenhaApp { get; set; } = string.Empty;
        public string SmtpServer { get; set; } = string.Empty;
        public int Porta { get; set; }
    }
}
