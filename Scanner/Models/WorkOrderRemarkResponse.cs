using Newtonsoft.Json;
using System.Collections.Generic;

namespace Scanner.Models
{
    public class WorkOrderRemarkResponse
    {
        [JsonProperty("success")]
        public bool Success
        {
            get;
            set;
        }

        [JsonProperty("urgent_count")]
        public int UrgentCount
        {
            get;
            set;
        }

        [JsonProperty("returned_count")]
        public int ReturnedCount
        {
            get;
            set;
        }

        [JsonProperty("has_more")]
        public bool HasMore
        {
            get;
            set;
        }

        [JsonProperty("start_timestamp")]
        public long StartTimestamp
        {
            get;
            set;
        }

        [JsonProperty("server_timestamp")]
        public long ServerTimestamp
        {
            get;
            set;
        }

        [JsonProperty("work_orders")]
        public List<WorkOrderRemark> WorkOrders
        {
            get;
            set;
        }
    }

    public class WorkOrderRemark
    {
        [JsonProperty("id")]
        public string Id
        {
            get;
            set;
        }

        [JsonProperty("sn")]
        public string Sn
        {
            get;
            set;
        }

        [JsonProperty("tracking_number")]
        public string TrackingNumber
        {
            get;
            set;
        }

        [JsonProperty("remark")]
        public string Remark
        {
            get;
            set;
        }

        [JsonProperty("remark_timestamp")]
        public long RemarkTimestamp
        {
            get;
            set;
        }

        [JsonProperty("is_urgent")]
        public bool IsUrgent
        {
            get;
            set;
        }
    }
}