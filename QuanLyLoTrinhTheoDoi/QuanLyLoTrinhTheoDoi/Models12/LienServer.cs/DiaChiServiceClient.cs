using Microsoft.Extensions.Logging;
using QuanLyLoTrinhTheoDoi.Models12.DieuPhoiLoTrinh;
using QuanLyLoTrinhTheoDoi.Models12.LienServer.cs;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer;
using QuanLyLoTrinhTheoDoi.Models12.ThongTinLienServer.DiaChi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace QuanLyLoTrinhTheoDoi.Models12.LienServer
{
    public class DiaChiServiceClient : IDiaChiService
    {
        private readonly HttpClient _client;
        private readonly ILogger<DiaChiServiceClient> _logger;
        private const string BaseUrl = "https://localhost:7149";

        public DiaChiServiceClient(HttpClient client, ILogger<DiaChiServiceClient> logger)
        {
            _client = client;
            _logger = logger;
        }

        /// <summary>
        /// 1. Gọi API lấy chi tiết thông tin một địa chỉ theo ID
        /// </summary>
        public async Task<DiaChiModel?> GetChiTietDiaChiAsync(int maDiaChi)
        {
            try
            {
                // Endpoint tương ứng với [HttpGet("chitietdiachi/{maDiaChi}")] bên Server Địa chỉ
                var response = await _client.GetAsync($"{BaseUrl}/api/quanlydiachi/chitietdiachi/{maDiaChi}");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<DiaChiModel>();
                }

                _logger.LogWarning($"Không tìm thấy thông tin địa chỉ ID: {maDiaChi}. Status: {response.StatusCode}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Lỗi kết nối API lấy chi tiết địa chỉ ID: {maDiaChi}");
                return null;
            }
        }

        /// <summary>
        /// 2. Gọi API lấy danh sách tọa độ (Vĩ độ, Kinh độ) theo danh sách ID địa chỉ gửi lên
        /// </summary>
        public async Task<List<ToaDoDiaChiResponseDto>?> GetToaDoDanhSachAsync(List<int> maDiaChis)
        {
            if (maDiaChis == null || !maDiaChis.Any())
            {
                _logger.LogWarning("Yêu cầu lấy danh sách tọa độ không hợp lệ: Danh sách mã địa chỉ trống.");
                return null;
            }

            try
            {
                // Endpoint tương ứng với [HttpPost("lay-toa-do-danh-sach")] bên Server Địa chỉ
                var response = await _client.PostAsJsonAsync($"{BaseUrl}/api/quanlydiachi/lay-toa-do-danh-sach", maDiaChis);

                if (response.IsSuccessStatusCode)
                {
                    // Đọc JSON phản hồi giải mã thành danh sách List<ToaDoResponseDto>
                    return await response.Content.ReadFromJsonAsync<List<ToaDoDiaChiResponseDto>>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("API Lấy danh sách tọa độ trả về lỗi. Status: {StatusCode}, Chi tiết: {Error}", response.StatusCode, errorContent);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi kết nối khi gọi API lấy danh sách tọa độ.");
                return null;
            }
        }

        
    }
}