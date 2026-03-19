using QuanLyTaiKhoanNguoiDung.Models;
using QuanLyTaiKhoanNguoiDung.Models12.QuanLyNguoiDung.QuanLyLichLamViec;

namespace QuanLyTaiKhoanNguoiDung.Services
{
    public interface IAISchedulingService
    {
        DangKyCaTrucViewModel AnalyzeShift(DangKyCaTruc dk, List<DangKyCaTruc> lichSuCuaTaiXe);
    }

    public class AISchedulingService : IAISchedulingService
    {
        public DangKyCaTrucViewModel AnalyzeShift(DangKyCaTruc dk, List<DangKyCaTruc> lichSuCuaTaiXe)
        {
            double score = 7.0; // Điểm gốc
            var reasons = new List<string>();
            var taiXe = dk.MaNguoiDungNavigation?.TaiXe;

            // 1. Kiểm tra Bằng lái (Hard Rule)
            if (taiXe != null && taiXe.NgayHetHanBang < dk.NgayTruc)
            {
                score = 0;
                reasons.Add("Bằng lái hết hạn.");
            }

            // 2. Kiểm tra Nghỉ ngơi (Safety Rule)
            var caTruoc = lichSuCuaTaiXe
                .Where(x => x.NgayTruc == dk.NgayTruc.AddDays(-1))
                .OrderByDescending(x => x.MaCa)
                .FirstOrDefault();

            if (caTruoc != null)
            {
                bool caTruocDem = caTruoc.MaCaNavigation?.TenCa?.Contains("Đêm") ?? false;
                bool caNaySang = dk.MaCaNavigation?.TenCa?.Contains("Sáng") ?? false;
                if (caTruocDem && caNaySang)
                {
                    score -= 4.0;
                    reasons.Add("Vi phạm nghỉ ngơi (Đêm trước vừa trực).");
                }
            }

            // 3. Điểm Uy tín & Kinh nghiệm
            if (taiXe != null)
            {
                if (taiXe.DiemUyTin < 50) { score -= 2.0; reasons.Add("Uy tín thấp."); }
                else if (taiXe.DiemUyTin > 85) { score += 1.0; reasons.Add("Uy tín cao."); }

                if (taiXe.KinhNghiemNam >= 10) { score += 1.5; reasons.Add("Chuyên gia (>10 năm)."); }
            }

            // 4. Chuẩn hóa & Khuyến nghị
            score = Math.Clamp(score, 0, 10);
            string recommendation = score >= 8.0 ? "Ưu tiên" : (score >= 5.0 ? "Hợp lệ" : "Cần cân nhắc");

            return new DangKyCaTrucViewModel
            {
                MaDangKy = dk.MaDangKy,
                TenTaiXe = dk.MaNguoiDungNavigation?.HoTenNhanVien ?? "N/A",
                TenCa = dk.MaCaNavigation?.TenCa ?? "N/A",
                NgayTruc = dk.NgayTruc,
                AI_Score = Math.Round(score, 1),
                AI_Recommendation = recommendation,
                AI_Reasons = reasons
            };
        }
    }
}