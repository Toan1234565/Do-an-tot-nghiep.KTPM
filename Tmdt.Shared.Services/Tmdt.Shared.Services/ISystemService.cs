using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tmdt.Shared.Services
{
    public interface ISystemService
    {
        // Hàm dùng chung để ghi log và reset cache
        Task GhiLogVaResetCacheAsync(string dichVu, string thaoTac, string bang, string maDoiTuong, object dataCu, object dataMoi);

        // Hàm lấy ID người dùng đang đăng nhập
        int? GetCurrentUserId();
    }
}
