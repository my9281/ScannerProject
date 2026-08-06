using Scanner.Models;
using Scanner.Services;
using System;
using System.Threading.Tasks;

namespace Scanner.Helpers
{
    public sealed class NetworkHelper
    {
        private readonly WorkOrderRemarkService _workOrderService;
        public NetworkHelper() : this(new WorkOrderRemarkService())
        {
        }

        internal NetworkHelper(WorkOrderRemarkService workOrderService)
        {
            _workOrderService = workOrderService ?? throw new ArgumentNullException(nameof(workOrderService));
        }

        public Task<WorkOrderRemarkResponse> GetWorkOrderRemarksAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new UnauthorizedAccessException("没有登录 Token，请重新登录。");
            }
            return _workOrderService.GetRemarksAsync(token);
        }
    }
}
