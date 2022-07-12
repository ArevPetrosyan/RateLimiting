namespace RateLimiting.Models
{
    public class RuleModel
    {
        public string CountryCode { get; set; }
        public int MaxRequests { get; set; }
        public int TimeLimitation { get; set; }
    }
}
