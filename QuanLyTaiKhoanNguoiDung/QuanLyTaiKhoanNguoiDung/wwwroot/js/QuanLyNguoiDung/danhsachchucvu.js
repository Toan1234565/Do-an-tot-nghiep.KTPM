document.addEventListener("DOMContentLoaded", function () {
    // Gọi hàm load chức vụ khi trang web đã sẵn sàng
    loadChucVu();
});

async function loadChucVu() {
    const selectElement = document.getElementById('selectChucVu');
    const apiUrl = 'https://localhost:7022/api/quanlynguoidung/danhsachchucvu';

    try {
        const response = await fetch(apiUrl);

        if (!response.ok) {
            if (response.status === 404) {
                console.warn("Không tìm thấy danh sách chức vụ.");
            }
            throw new Error('Lỗi khi gọi API: ' + response.statusText);
        }

        const data = await response.json();

        // Xóa các option cũ (trừ option mặc định đầu tiên)
        selectElement.innerHTML = '<option value="">-- Chọn chức vụ --</option>';

        // Duyệt qua danh sách và tạo thẻ <option>
        data.forEach(item => {
            const option = document.createElement('option');
            option.value = item.maChucVu; // Lưu ý: JS thường trả về camelCase (maChucVu thay vì MaChucVu)
            option.textContent = item.tenChucVu;
            selectElement.appendChild(option);
        });

    } catch (error) {
        console.error('Đã xảy ra lỗi:', error);
        // Có thể hiển thị thông báo lỗi lên giao diện nếu cần
    }
}