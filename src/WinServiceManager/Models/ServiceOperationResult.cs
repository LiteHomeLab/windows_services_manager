namespace WinServiceManager.Models
{
    /// <summary>
    /// 服务操作结果
    /// </summary>
    public class ServiceOperationResult
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误信息（如果操作失败）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 详细的错误信息
        /// </summary>
        public string? Details { get; set; }

        /// <summary>
        /// 操作类型
        /// </summary>
        public ServiceOperationType Operation { get; set; }

        /// <summary>
        /// 操作耗时（毫秒）
        /// </summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static ServiceOperationResult SuccessResult(ServiceOperationType operation, long elapsedMs = 0)
        {
            return new ServiceOperationResult
            {
                Success = true,
                Operation = operation,
                ElapsedMilliseconds = elapsedMs
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static ServiceOperationResult FailureResult(
            ServiceOperationType operation,
            string errorMessage,
            string? details = null,
            long elapsedMs = 0)
        {
            return new ServiceOperationResult
            {
                Success = false,
                Operation = operation,
                ErrorMessage = errorMessage,
                Details = details,
                ElapsedMilliseconds = elapsedMs
            };
        }
    }

    /// <summary>
    /// 服务操作类型
    /// </summary>
    public enum ServiceOperationType
    {
        Install,
        Uninstall,
        Start,
        Stop,
        Restart,
        Update,
        QueryStatus
    }
}