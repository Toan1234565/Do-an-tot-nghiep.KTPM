namespace QuanLyKho.Models1
{
    public class LocThayDoi
    {
        // Phải để kiểu trả về rõ ràng là Tuple các Dictionary để bên ngoài dùng được var (diffCu, diffMoi)
        public static (Dictionary<string, object> oldDiff, Dictionary<string, object> newDiff) GetChanges(
            Dictionary<string, object> oldData,
            Dictionary<string, object> newData)
        {
            var oldDiff = new Dictionary<string, object>();
            var newDiff = new Dictionary<string, object>();

            foreach (var key in oldData.Keys)
            {
                // Ép kiểu về string để so sánh nội dung cho chính xác (tránh lỗi so sánh tham chiếu object)
                var valOld = oldData[key]?.ToString();
                var valNew = newData.ContainsKey(key) ? newData[key]?.ToString() : null;

                if (valOld != valNew)
                {
                    oldDiff.Add(key, oldData[key] ?? "Trống");
                    newDiff.Add(key, newData[key] ?? "Trống");
                }
            }
            return (oldDiff, newDiff);
        }
    }
}
