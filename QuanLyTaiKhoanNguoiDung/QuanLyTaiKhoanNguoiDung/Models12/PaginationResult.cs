namespace QuanLyTaiKhoanNguoiDung.Models12
{
    public class PaginationResult<T>
    {
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalRecords { get; set; }
        public List<T> Data { get; set; } = new List<T>();
    }
}
