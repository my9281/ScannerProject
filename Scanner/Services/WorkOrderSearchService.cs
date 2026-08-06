using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scanner.Services
{
    public sealed class WorkOrderSearchService
    {
        private List<WorkOrderRemark> _workOrders = new List<WorkOrderRemark>();

        public int Count => _workOrders.Count;

        public void Replace(IEnumerable<WorkOrderRemark> workOrders)
        {
            _workOrders = workOrders == null
                ? new List<WorkOrderRemark>()
                : workOrders.Where(item => item != null).ToList();
        }

        public void MergeImported(IEnumerable<WorkOrderRemark> workOrders)
        {
            if (workOrders == null)
            {
                return;
            }

            foreach (WorkOrderRemark item in workOrders.Reverse())
            {
                if (item == null)
                {
                    continue;
                }

                _workOrders.RemoveAll(existing => HasSameIdentity(existing, item));
                _workOrders.Insert(0, item);
            }
        }

        public WorkOrderRemark Find(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            return _workOrders.FirstOrDefault(item =>
                EqualsCode(item.Sn, code) || EqualsCode(item.TrackingNumber, code));
        }

        private static bool HasSameIdentity(WorkOrderRemark left, WorkOrderRemark right)
        {
            return left != null && right != null &&
                ((!string.IsNullOrWhiteSpace(right.Sn) && EqualsCode(left.Sn, right.Sn)) ||
                 (!string.IsNullOrWhiteSpace(right.TrackingNumber) &&
                  EqualsCode(left.TrackingNumber, right.TrackingNumber)));
        }

        private static bool EqualsCode(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
