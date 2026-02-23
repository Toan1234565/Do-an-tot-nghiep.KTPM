document.addEventListener("DOMContentLoaded", function () {
    const form = document.getElementById('formThemNhanVien');

    form.addEventListener('submit', async function (e) {
        e.preventDefault();

        // Thu thập dữ liệu từ Form
        const formData = new FormData(form);
        const payload = Object.fromEntries(formData.entries());

        // Hiển thị trạng thái đang xử lý
        Swal.fire({
            title: 'Đang xử lý...',
            didOpen: () => { Swal.showLoading(); }
        });
        try {
            const response = await fetch('https://localhost:7022/api/quanlynguoidung/themnhanvien', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });

            const result = await response.json();

            if (response.ok) {
                // THÀNH CÔNG
                Swal.fire({
                    icon: 'success',
                    title: 'Thành công!',
                    text: result.message,
                    timer: 100,
                    showConfirmButton: false
                }).then(() => {
                    // Đóng modal và tải lại trang để cập nhật danh sách
                    const modal = bootstrap.Modal.getInstance(document.getElementById('ThemNhanVienModal'));
                    modal.hide();
                    location.reload();
                    window.location.reload();
                });
            } else {
                // THẤT BẠI (Ví dụ: Trùng tên đăng nhập)
                Swal.fire({
                    icon: 'error',
                    title: 'Thất bại',
                    text: result.message || 'Có lỗi xảy ra'
                });
            }
        } catch (error) {
            Swal.fire({
                icon: 'error',
                title: 'Lỗi kết nối',
                text: 'Không thể kết nối tới máy chủ.'
            });
        }
    });
});