// Hàm Vô hiệu hóa
function vôHiệuHóa(maNguoiDung) {
    Swal.fire({
        title: 'Xác nhận vô hiệu hóa?',
        text: "Nhập lý do khóa tài khoản:",
        input: 'text',
        inputPlaceholder: 'Lý do (ví dụ: Vi phạm điều khoản)...',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#d33',
        cancelButtonColor: '#3085d6',
        confirmButtonText: 'Vô hiệu hóa ngay',
        cancelButtonText: 'Hủy',
        preConfirm: (lyDo) => {
            if (!lyDo) {
                Swal.showValidationMessage('Vui lòng nhập lý do khóa!');
            }
            return lyDo;
        }
    }).then((result) => {
        if (result.isConfirmed) {
            // Gọi API
            fetch(`/api/quanlytaikhoan/vohieuhoa/${maNguoiDung}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ LyDo: result.value })
            })
                .then(response => response.json())
                .then(data => {
                    if (data.message.includes("thành công")) {
                        Swal.fire('Đã khóa!', data.message, 'success').then(() => location.reload());
                    } else {
                        Swal.fire('Lỗi!', data.message, 'error');
                    }
                })
                .catch(error => Swal.fire('Lỗi!', 'Không thể kết nối tới server', 'error'));
        }
    });
}

// Hàm Kích hoạt lại
function kichHoat(maNguoiDung) {
    Swal.fire({
        title: 'Kích hoạt lại?',
        text: "Tài khoản này sẽ có thể đăng nhập lại vào hệ thống.",
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#28a745',
        cancelButtonColor: '#3085d6',
        confirmButtonText: 'Kích hoạt',
        cancelButtonText: 'Hủy'
    }).then((result) => {
        if (result.isConfirmed) {
            // Gọi API
            fetch(`/api/quanlytaikhoan/mokhoa/${maNguoiDung}`, {
                method: 'PUT'
            })
                .then(response => response.json())
                .then(data => {
                    if (data.message.includes("thành công")) {
                        Swal.fire('Thành công!', data.message, 'success').then(() => location.reload());
                    } else {
                        Swal.fire('Lỗi!', data.message, 'error');
                    }
                })
                .catch(error => Swal.fire('Lỗi!', 'Không thể kết nối tới server', 'error'));
        }
    });
}