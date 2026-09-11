using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scanner.WPF.Services
{
    public sealed class WorkOrderSearchService
    {
        private List<WorkOrderRemark> _onlineWorkOrders = new List<WorkOrderRemark>();
        private List<WorkOrderRemark> _importedRules = new List<WorkOrderRemark>();
        private WorkOrderRemark _activeOidRule;
        public int Count => _onlineWorkOrders.Count + _importedRules.Count;

        public void Replace(IEnumerable<WorkOrderRemark> workOrders)
        {
            _onlineWorkOrders = workOrders == null ? new List<WorkOrderRemark>() : workOrders.Where(item => item != null).ToList();
        }

        public void ReplaceImported(IEnumerable<WorkOrderRemark> rules)
        {
            _importedRules = rules == null ? new List<WorkOrderRemark>() : rules.Where(item => item != null).ToList();
            _activeOidRule = null;
        }

        public WorkOrderMatch Resolve(string code, bool isOid)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }
            string normalized = NormalizeCode(code);
            if (isOid)
            {
                _activeOidRule = _importedRules.Where(item => item.IsOidRule && ContainsCode(normalized, item.TrackingNumber)).OrderByDescending(item => NormalizeCode(item.TrackingNumber).Length).FirstOrDefault();
                if (_activeOidRule != null)
                {
                    return new WorkOrderMatch(_activeOidRule, false);
                }
                WorkOrderRemark exactOidMatch = FindExact(normalized);
                if (exactOidMatch != null && !exactOidMatch.IsOidRule)
                {
                    return new WorkOrderMatch(exactOidMatch, true);
                }
                return null;
            }
            if (_activeOidRule != null)
            {
                return new WorkOrderMatch(_activeOidRule, false);
            }
            WorkOrderRemark exactMatch = FindExact(normalized);
            if (exactMatch != null)
            {
                return new WorkOrderMatch(exactMatch, true);
            }
            return null;
        }

        private WorkOrderRemark FindExact(string normalizedCode)
        {
            WorkOrderRemark importedSn = _importedRules.FirstOrDefault(item => !item.IsOidRule && EqualsCode(item.Sn, normalizedCode));
            if (importedSn != null)
            {
                return importedSn;
            }
            return _onlineWorkOrders.FirstOrDefault(item => EqualsCode(item.Sn, normalizedCode) || EqualsCode(item.TrackingNumber, normalizedCode));
        }

        private static bool ContainsCode(string oid, string trackingNumber)
        {
            string tracking = NormalizeCode(trackingNumber);
            return !string.IsNullOrEmpty(oid) && !string.IsNullOrEmpty(tracking) && oid.IndexOf(tracking, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EqualsCode(string left, string normalizedRight)
        {
            return !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(normalizedRight) && string.Equals(NormalizeCode(left), normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character) && character != '\uFEFF' && character != '\u200B')
                {
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }
    }

    public sealed class WorkOrderMatch
    {
        public WorkOrderMatch(WorkOrderRemark workOrder, bool useMatchedSn)
        {
            WorkOrder = workOrder ?? throw new ArgumentNullException(nameof(workOrder));
            UseMatchedSn = useMatchedSn;
        }

        public WorkOrderRemark WorkOrder { get; }
        public bool UseMatchedSn { get; }

        public string GetPrintCode(string scannedCode)
        {
            return UseMatchedSn && !string.IsNullOrWhiteSpace(WorkOrder.Sn) ? WorkOrderSearchService.NormalizeCode(WorkOrder.Sn) : WorkOrderSearchService.NormalizeCode(scannedCode);
        }
    }
}
