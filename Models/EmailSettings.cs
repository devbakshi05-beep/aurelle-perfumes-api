namespace PerfumeShop.Api.Models
{
    // appsettings.json ke "Email" block se bharta hai
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 587;

        // Gmail jisse SMTP login hota hai
        public string User { get; set; } = string.Empty;

        // Google ka 16 letter App Password (normal Gmail password nahi)
        public string AppPassword { get; set; } = string.Empty;

        // official sender address (khali ho toh User wala use hota hai)
        public string FromEmail { get; set; } = string.Empty;

        public string FromName { get; set; } = string.Empty;

        // customer reply kare toh kahan jaaye (optional)
        public string ReplyTo { get; set; } = string.Empty;

        // order email ki copy (kai email ho toh comma se alag karo)
        public string CcEmail { get; set; } = string.Empty;

        // chhupi hui copy (optional)
        public string BccEmail { get; set; } = string.Empty;

        // sirf testing: bharoge toh SAARI emails sirf isi inbox mein jaayengi. Asli use mein khali rakho
        public string TestToEmail { get; set; } = string.Empty;
    }
}
