namespace WinServiceManager.Dialogs
{
    /// <summary>
    /// 保存文件对话框接口
    /// </summary>
    public interface ISaveFileDialog
    {
        /// <summary>
        /// 文件过滤器
        /// </summary>
        string Filter { get; set; }

        /// <summary>
        /// 默认文件名
        /// </summary>
        string FileName { get; set; }

        /// <summary>
        /// 初始目录
        /// </summary>
        string InitialDirectory { get; set; }

        /// <summary>
        /// 标题
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// 显示对话框并返回结果
        /// </summary>
        /// <returns>如果用户点击确定则为true，否则为false</returns>
        bool ShowDialog();
    }
}