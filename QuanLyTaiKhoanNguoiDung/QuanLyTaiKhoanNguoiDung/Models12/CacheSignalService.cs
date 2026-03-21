namespace QuanLyTaiKhoanNguoiDung.Models12
{
    public class CacheSignalService
    {
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        public void Reset()
        {
            TokenSource.Cancel();
            TokenSource = new CancellationTokenSource();
        }
    }
}
