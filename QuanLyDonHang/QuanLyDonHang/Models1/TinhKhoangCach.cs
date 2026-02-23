namespace QuanLyDonHang.Models1
{
    public class TinhKhoangCach
    {
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Bán kính Trái đất tính bằng Kilomet
            const double EarthRadius = 6371;

            // Chuyển đổi sang Radian
            double dLat = ToRadian(lat2 - lat1);
            double dLon = ToRadian(lon2 - lon1);

            // Công thức Haversine
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadian(lat1)) * Math.Cos(ToRadian(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadius * c;
        }

        private static double ToRadian(double val)
        {
            return (Math.PI / 180) * val;
        }
    }
}
