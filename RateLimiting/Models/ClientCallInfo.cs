using System;

namespace RateLimiting.Models
{
    public class ClientCallInfo
    {
        public DateTime LastSuccessfulResponseTime { get; set; }
        public int NumberOfRequestsCompletedSuccessfully { get; set; }
    }
}
