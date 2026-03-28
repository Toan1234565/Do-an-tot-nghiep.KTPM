namespace QuanLyDonHang.Models1
{
    public class MomoIPNRequest
    {
        public string? partnerCode { get; set; }    // Mã đối tác (Partner ID)
        public int orderId { get; set; }        // Mã đơn hàng phía bạn gửi sang
        public string? requestId { get; set; }      // Mã ID duy nhất của yêu cầu
        public long? amount { get; set; }           // Số tiền thanh toán
        public string? orderInfo { get; set; }      // Thông tin mô tả đơn hàng
        public string? orderType { get; set; }      // Loại đơn hàng (thường là momo_wallet)
        public string? transId { get; set; }          // MÃ GIAO DỊCH CỦA MOMO (Cực kỳ quan trọng để đối soát)
        public int resultCode { get; set; }        // Mã kết quả (0: Thành công, khác 0: Lỗi)
        public string? message { get; set; }        // Thông báo lỗi/thành công từ Momo
        public string? payType { get; set; }        // Hình thức thanh toán (web, app, qr...)
        public long responseTime { get; set; }     // Thời gian phản hồi (Unix timestamp)
        public string? extraData { get; set; }      // Dữ liệu bổ sung bạn gửi đi lúc đầu
        public string? signature { get; set; }      // CHỮ KÝ BẢO MẬT (Dùng để kiểm tra tin nhắn thật/giả)
    }

}
