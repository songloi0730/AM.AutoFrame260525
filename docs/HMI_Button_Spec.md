# HMI_Button_Spec — v2.0

Đặc tả chuẩn hoá mọi phần tử tương tác trên màn Home (kèm Persistent Frame).
Cột "Điều kiện" = precondition để nút khả dụng; không đạt → nút mờ, bấm hiện toast nêu lý do.
Role: OP = Operator, EN = Engineer, AD = Admin (OP+ nghĩa là Operator trở lên).

## 1. Header

| Nút | Điều kiện | Hành động khi bấm | Mở ra | Role |
|-----|-----------|-------------------|-------|------|
| Badge AUTO/MANUAL/DRY | luôn | — | Popup giải thích chế độ hiện tại | OP+ |
| Badge LOCAL/REMOTE | luôn | — | Popup trạng thái GEM: communication state + control state, nút yêu cầu chuyển Local (nếu host cho phép) | OP+ / chuyển: EN |
| Badge trạng thái PackML | luôn | — | Popup sơ đồ state machine, tô sáng trạng thái hiện tại, liệt kê điều kiện đang chặn transition | OP+ |
| Recipe {name} v{ver} | luôn | Điều hướng | Tab Recipe, recipe đang nạp được chọn, panel so sánh giá trị đang chạy vs file | OP+ |
| 🌐 Tiếng Việt ▾ | luôn | Mở dropdown | Danh sách ngôn ngữ bằng chính ngôn ngữ đó; chọn → đổi tức thời qua ILocalizationService | OP+ |
| Avatar {Tên · Role} ▾ | luôn | Mở dialog | Identity: đăng nhập PIN/RFID, đổi ca, đăng xuất; đổi user → khoá/mở nút theo role mới ngay | tất cả |

## 2. Tab điều hướng

| Tab | Điều kiện hiển thị | Mở ra | Role vào |
|-----|--------------------|-------|----------|
| Home | luôn | Màn Home (tài liệu Template §2–3) | OP+ |
| Vision | máy có camera (VisionLayout ≠ None) | Live view + VisionTeachView per camera | xem OP+ / teach EN+ |
| Motion/IO | luôn | AxisControlView + SensorVacuumMonitor | EN+ (xem IO: OP+) |
| Recipe | luôn | Danh sách + editor recipe | xem OP+ / sửa EN+ |
| Dữ liệu | luôn | Báo cáo sản lượng, Pareto NG, trend, xuất file | OP+ |
| Alarm | luôn | Active + History + ErrorDetailView | OP+ |
| Log | luôn | Bảng log đầy đủ, lọc, tìm, xuất | OP+ |
| Cài đặt | luôn (nội dung gate theo role) | GridMenuView theo config catalog | EN+ / hardware-host: AD |

## 3. Banner alarm

| Phần tử | Điều kiện | Hành động | Role |
|---------|-----------|-----------|------|
| Nút ACK | có alarm chưa ACK | ACK alarm đang hiển thị → alarm ưu tiên kế tiếp trồi lên; ghi user + giờ vào lịch sử | OP+ |
| Text alarm | có alarm | Mở ErrorDetailView của đúng alarm đó | OP+ |
| Chip +N khác ▾ | ≥2 alarm chưa ACK | Bung danh sách alarm theo ưu tiên / nhảy tab Alarm | OP+ |

## 4. Vùng làm việc

| Phần tử | Điều kiện | Hành động | Mở ra | Role |
|---------|-----------|-----------|-------|------|
| Sub-tab (Sản phẩm, Lực vít, Vị trí…) | theo HomeSubViews | Chuyển view trong content region, nhớ lựa chọn | — | OP+ |
| Thumbnail camera | camera cấu hình | Phóng to ảnh kết quả | View phóng to + nút Teach (→VisionTeachView, EN+) + nút Lưu ảnh (kèm SN) | OP+ |
| Dòng bảng sản phẩm | có dữ liệu | Mở popup chi tiết | Toàn bộ phép đo theo trạm + ảnh vision từng camera + nút "Gửi lại MES" (EN+) | OP+ |
| Lọc Tất cả / Chỉ NG | có dữ liệu | Lọc bảng tại chỗ | — | OP+ |
| Điểm vít trên bản đồ (ScrewForceChart) | sub-tab active | Đổi đường lực sang vít được chọn | — | OP+ |
| Điểm vị trí (WorkPositionMap) | sub-tab active | Hiện đường lực/giá trị đo của vị trí | — | OP+ |

## 5. Right rail — Thao tác nhanh (theo QuickActions config)

| Nút (ví dụ máy SCR) | Loại | Điều kiện/Interlock | Hành động | Audit | Role |
|----------------------|------|---------------------|-----------|-------|------|
| 💡 Đèn máy | Toggle | luôn | IO.WorkLight on/off, nút hiển thị trạng thái | — | OP+ |
| 🔕 Tắt còi | Momentary | có còi đang kêu | Tắt buzzer/âm tower lamp. KHÔNG ACK alarm | — | OP+ |
| 🔓 Mở cửa an toàn | Hold 1s | StateIn(IDLE, PAUSED, STOPPED); nếu EXECUTE → kích chuỗi dừng-an-toàn trước | Safety.RequestDoorUnlock; nút hiển thị tiến trình Yêu cầu → Đang dừng → Đã mở khoá | ✔ | OP+ |
| 📦 Cửa cấp liệu | Hold 1s | StationIdle(Loader) | IO.FeedDoorUnlock | ✔ | OP+ |
| 🌀 Thổi ion | Momentary | luôn | Chu kỳ ionizer 30 s | — | OP+ |
| 🧰 Gọi kỹ thuật | Momentary | luôn | Nháy tower lamp / tín hiệu Andon | ✔ | OP+ |

## 6. Right rail — khác

| Phần tử | Hành động | Mở ra | Role |
|---------|-----------|-------|------|
| Dòng trạm (Loader…) | Mở popup trạm | Mechanism con, bước sequence, lỗi gần nhất, nút Manual theo trạm (EN+) | OP+ |
| Cửa an toàn / E-Stop | chỉ hiển thị (tín hiệu phần cứng qua HardwareInputEventBus) | — | — |
| Dòng nhật ký | Bung toàn văn message | — | OP+ |
| "xem tất cả →" | Điều hướng | Tab Log | OP+ |

## 7. Action bar

| Nút | Khả dụng từ (CanFire) | Hành động | Popup/Mở ra | Role |
|-----|------------------------|-----------|-------------|------|
| ▶ Start | IDLE + pre-check đạt | STARTING → EXECUTE | Pre-check fail: popup liệt kê điều kiện thiếu + nút nhảy tới chỗ xử lý | OP+ |
| ⏸ Pause / Resume | EXECUTE / PAUSED | Dừng điểm an toàn gần nhất, giữ chân không + vị trí; nhãn tự đổi | — | OP+ |
| ⏹ Stop | EXECUTE, PAUSED | STOPPING → STOPPED | Popup chọn: Dừng hết chu kỳ / Dừng ngay | OP+ |
| ↺ Reset | STOPPED, ABORTED, COMPLETE và mọi alarm đã ACK | RESETTING → IDLE, xoá cờ lỗi, tư thế an toàn | Còn alarm chưa ACK: toast + nhảy tab Alarm | OP+ |
| Dry run | toggle khi IDLE | Chế độ chạy không vật liệu, vision giả lập, badge → DRY | — | EN+ |
| Manual | không ở EXECUTE | Overlay ManualControlView toàn màn; thoát qua Reset | ManualControlView | EN+ |

Khi REMOTE: toàn bộ action bar khoá trừ Stop (và thao tác an toàn) — toast "Máy đang do host điều khiển".

## 8. Thanh kết nối

| Phần tử | Hành động | Mở ra | Role |
|---------|-----------|-------|------|
| Chip thiết bị/host | Mở popup chẩn đoán | Trạng thái, địa chỉ/cổng, thống kê truyền thông (retry, độ trễ, lần mất gần nhất), nút Test, nút Reconnect (EN+). SECS/GEM: thêm communication state + control state | OP+ / Reconnect EN+ |
| Chuỗi phiên bản | — (chỉ hiển thị) | Chi tiết đầy đủ ở Cài đặt → Giới thiệu | — |

---

*Quy tắc chung: mọi nút bị khoá đều mờ + toast lý do khi bấm (không ẩn). Mọi hành động có hậu quả vật lý hoặc đổi dữ liệu đều ghi audit log: user, thời gian, lệnh, kết quả.*
