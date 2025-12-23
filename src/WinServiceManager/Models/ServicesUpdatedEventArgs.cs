using System;
using System.Collections.Generic;
using WinServiceManager.Models;

namespace WinServiceManager.Models
{
    /// <summary>
    /// 服务批量状态更新事件参数
    /// </summary>
    public class ServicesUpdatedEventArgs : EventArgs
    {
        /// <summary>
        /// 服务ID到状态的映射字典
        /// </summary>
        public Dictionary<string, ServiceStatus> StatusUpdates { get; set; } = new();
    }
}
