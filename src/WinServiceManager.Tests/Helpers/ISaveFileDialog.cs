namespace WinServiceManager.Tests.Helpers
{
    /// <summary>
    /// Interface for mocking SaveFileDialog in tests
    /// </summary>
    public interface ISaveFileDialog
    {
        bool? ShowDialog();
        string FileName { get; set; }
        string Title { get; set; }
        string Filter { get; set; }
        string InitialDirectory { get; set; }
    }
}