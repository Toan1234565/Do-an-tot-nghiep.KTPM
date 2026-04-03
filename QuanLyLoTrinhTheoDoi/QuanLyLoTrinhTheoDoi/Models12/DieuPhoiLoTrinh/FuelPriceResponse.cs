namespace QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh
{
    public class FuelPriceResponse
    {
        // Tên thuộc tính phải khớp với Key trong JSON của API
        // Nếu API trả về "diesel_price", hãy dùng [JsonPropertyName("diesel_price")]
        public decimal DieselPrice { get; set; }
        public decimal GasolinePrice { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? Region { get; set; }
    }
}
