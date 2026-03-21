// 1. Hàm Vô hiệu hóa tài xế (Khóa tài khoản)
async function vôHiệuHóa(maNguoiDung) {
    const { value: lyDo } = await Swal.fire({
        title: 'Xác nhận vô hiệu hóa?',
        text: "Nhập lý do khóa tài khoản tài xế:",
        input: 'text',
        inputPlaceholder: 'Ví dụ: Vi phạm quy định an toàn, nghỉ việc...',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#3085d6',
        confirmButtonText: 'Vô hiệu hóa ngay',
        cancelButtonText: 'Hủy',
        preConfirm: (value) => {
            if (!value) {
                Swal.showValidationMessage('Vui lòng nhập lý do khóa!');
            }
            return value;
        }
    });

    if (lyDo) {
        try {
            // Hiển thị loading trong khi chờ API và Email/RabbitMQ xử lý
            Swal.fire({ title: 'Đang xử lý...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

            const response = await fetch(`https://localhost:7022/api/quanlytaikhoan/vohieuhoa/${maNguoiDung}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ LyDo: lyDo }) // Đảm bảo khớp với Class KhoaTaiKhoanRequest ở Backend
            });

            const data = await response.json();

            if (response.ok) {
                await Swal.fire({
                    title: 'Đã khóa!',
                    text: data.message || 'Tài khoản đã được vô hiệu hóa thành công.',
                    icon: 'success',
                    timer: 1500,
                    showConfirmButton: false
                });
                location.reload(); // Tải lại trang để cập nhật UI từ Cache mới
            } else {
                throw new Error(data.message || 'Lỗi từ phía máy chủ');
            }
        } catch (error) {
            console.error("Lỗi fetch:", error);
            Swal.fire('Thất bại', error.message, 'error');
        }
    }
}

// 2. Hàm Kích hoạt lại tài xế (Mở khóa)
function kichHoat(maNguoiDung) {
    Swal.fire({
        title: 'Kích hoạt lại tài xế?',
        text: "Tài xế này sẽ có thể tiếp nhận các chuyến hàng mới.",
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#28a745',
        cancelButtonColor: '#3085d6',
        confirmButtonText: 'Kích hoạt ngay',
        cancelButtonText: 'Hủy'
    }).then(async (result) => {
        if (result.isConfirmed) {
            try {
                // Hiển thị loading
                Swal.fire({ title: 'Đang kích hoạt...', allowOutsideClick: false, didOpen: () => { Swal.showLoading(); } });

                const response = await fetch(`https://localhost:7022/api/quanlytaikhoan/mokhoa/${maNguoiDung}`, {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json' }
                });

                const data = await response.json();

                if (response.ok) {
                    await Swal.fire({
                        title: 'Thành công!',
                        text: data.message || 'Tài khoản đã hoạt động trở lại.',
                        icon: 'success',
                        timer: 1500,
                        showConfirmButton: false
                    });
                    location.reload(); // Tải lại trang
                } else {
                    throw new Error(data.message || 'Không thể mở khóa tài khoản');
                }
            } catch (error) {
                console.error("Lỗi fetch:", error);
                Swal.fire('Lỗi!', error.message, 'error');
            }
        }
    });
}