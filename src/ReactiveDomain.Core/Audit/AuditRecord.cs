using System;
namespace ReactiveDomain.Audit
{
    public class AuditRecord
    {
        public int Version = 0;
        public Guid PolicyUserId { get; set; }
        public Guid CommitId { get; set; }
        public string AggregateName { get; set; }
        public string EventName { get; set; }
        public DateTime EventDateUTC { get; set; }
    }
}
