# HMI Advanced Standards — SEMI E95 · EEMUA 201 · ISA-18.2 · Siemens Workbook (lọc cho IPC 21–24")

> **Mục đích:** chắt lọc các chuẩn/sổ tay HMI nâng cao và **quyết định cái nào áp / không áp** cho dự án —
> máy tự động hoá trên **IPC 21–24" / 1920×1080, chuột + cảm ứng**, máy đơn lẻ (không phải SCADA nhiều khu vực).
> Đọc kèm: `.claude/skills/am-hmi-design/SKILL.md` (rule chính) · `docs/HMI_UI_Architecture_Template.md` · `docs/HMI_Components_Catalog.md`.
>
> **Nguyên tắc vàng khi đọc mọi tài liệu HMI:** *"Khuyến nghị này viết cho cỡ màn nào?"* — phần lớn best-practice
> ("nút thật to", "một chức năng/màn", "ẩn bớt thông tin", "<30% mật độ") viết cho **panel 7–12"**. Trên IPC 21–24"
> ta có nhiều không gian → **bố cục nhiều cột, hiện nhiều thông tin có tổ chức**, nút theo chuẩn chạm nhưng không cần to như panel.

---

## 1. Quyết định adoption (phê phán — cái gì KHÔNG bê nguyên)

| Nội dung | Quyết định | Lý do |
|----------|-----------|-------|
| **Bố cục 4-panel SEMI E95** (Nav ở **đáy**, Command Panel cột **phải**) | ❌ **Không** (giữ ISA-101) | Mâu thuẫn layout đã dựng (nav trái, lệnh ở header, alarm+status bar dưới). Cả 2 đều hợp lệ — chọn 1, nhất quán. **Chỉ theo 4-panel nếu khách bán dẫn yêu cầu chứng nhận SEMI E95.** |
| **Không cho minimize/close cửa sổ chính** | 🔶 **Option lúc deploy** | Đúng cho IPC production (kiosk: `WindowState=Maximized` + `WindowStyle=None` hoặc chặn close). KHÔNG bật lúc dev/laptop (đã sửa resize được vì tràn màn scale 125%). |
| **4 nền xám theo cấp** (`#F0F0F0…#C0C0C0`) | 🔶 **Không bắt buộc** | Hợp SCADA nhiều area. Máy 1 IPC: 1 light theme + panel/section là đủ; phân cấp thể hiện qua title/breadcrumb. |
| **Mật độ <30%, khoảng trắng 40–60%** | ⚠️ **Không giáo điều** | Viết cho overview process plant. Màn IO/Motion chi tiết trên 24" nên hiện **nhiều cột, đủ dữ liệu**; giữ khoảng trắng để gom nhóm, không hy sinh thông tin kỹ sư cần. |
| **ISA-18.2: <6 alarm/giờ, <30/10 phút** | ⚠️ **Nguyên tắc, bỏ số** | Cho nhà máy quy trình lớn. Máy đơn: giữ nguyên tắc (phân mức, đừng flood), bỏ ngưỡng cứng. |
| **Đỏ thuần `#FF0000`, vàng `#FFD700`** | ⚠️ **Dùng token giảm bão hoà** | `#FF0000` chói/rung mắt ca dài. Token hiện tại (`#F44336`/`#FFC107`…) dịu hơn, vẫn đủ tương phản. |
| **Nút theo input** (chuột 32px / chạm ≥44 / găng ≥60) | ✅ **Theo chạm** | Mouse+touch → "thiết kế cho input kém chính xác nhất": nút thường ≥44, lệnh chính ≥60. Nuance 32px (mouse-only) KHÔNG áp. |

---

## 2. Bổ sung định lượng NÊN áp (readability)

- **Tương phản chữ/nền ≥ 4.5:1**; alarm critical + thông tin an toàn **≥ 7:1**. Tránh vàng-trên-trắng, xám-nhạt-trên-xám.
- **Bậc cỡ chữ:** thường 12–14pt · giá trị quan trọng 16–18pt · **alarm/an toàn 20pt+**. Không TOÀN HOA, không in nghiêng dài.
- **Lưới 8px** (bội số 8/16/24/32/48); lề ≥ 16–32px. Khoảng trắng để **gom nhóm**, không để trống vô nghĩa.
- **Icon vector** (Segoe MDL2 / SVG) — WPF vốn vector + DPI-aware → sắc nét mọi scale (đã dùng trong Shell).
- **Demand vs Status:** luôn phân biệt rõ giá trị *đang là* (thực tế) với *đặt/yêu cầu* (setpoint) — lẫn 2 thứ này nguy hiểm.

---

## 3. SEMI E95 — ý CHỌN LỌC (áp được mà không cần 4-panel)

- **Salience** = viền màu báo trạng thái quanh đối tượng: **không viền = bình thường**; đỏ = alarm · vàng = caution ·
  xanh dương = đang xử lý/đang xem · xanh lá = cần chú ý (Ready to Load…). Viền **không che** đối tượng, **không** dùng cho on/off.
- **Nhãn nút Title-Case** ("Home All", "Load Recipe") — **không viết TOÀN HOA**.
- **Dialog box:** quy ước nút **OK / Cancel / Yes-No / Close / Apply** (đúng ngữ nghĩa); **disable trước** nút sẽ gây lỗi;
  icon trái + chữ phải (Information/Progress/Attention/Error); **bàn phím ảo** khi không có phím vật lý.
- **An toàn UX (mạnh):** **alarm/caution luôn truy cập được kể cả khi đang mở dialog**; **dialog KHÔNG che alarm bar / nav**.
- **Host status (SECS/GEM):** nếu theo SEMI E95 đặt ở Title Panel (góc trái) — hiện cả **Communication state + Control state**
  (ta đang để ở connection chips; chọn **một** chỗ, tránh trùng).

---

## 4. EEMUA 201 — nguyên tắc hệ thống (rất đáng theo)

- **Ưu tiên thiết kế cho tình huống BẤT THƯỜNG + khởi động/dừng**, không chỉ lúc bình thường — đúng lúc giao diện quan trọng nhất.
- **Overview thường trực** (Dashboard/khu overview luôn thấy) → tránh "tunnel vision" bỏ sót bất thường chỗ khác.
- **Alarm truy cập liên tục** mọi lúc.
- **Không "blank-screen syndrome":** đừng "chạy ngầm, chỉ báo khi lỗi" — operator cần thấy trạng thái liên tục để giữ "mental model" và nhận ra xu hướng xấu sớm.
- **Task-oriented display:** gom đủ tham số cho MỘT tác vụ vào 1 màn (wizard calib, setup recipe) thay vì bắt mở nhiều màn.
- **Animation/flashing dùng tiết kiệm**; mimic giữ tối thiểu chi tiết (dễ hiểu, không vẽ chi li như P&ID).
- **An toàn:** E-Stop **cứng độc lập** phòng khi HCI treo (ta có ISafetyInput + mạch an toàn phần cứng); có SOP cho mất điện/treo màn.

---

## 5. Siemens HMI Design Workbook — quy trình (process, không phải rule)

1. Lấy **người dùng làm trung tâm**; bắt đầu thiết kế UI **sớm**, song song cơ-điện.
2. **Hiểu bối cảnh:** liệt kê **use case theo vai trò** (Operator/Technician/Engineer) + tần suất → quyết định cái gì lên Dashboard,
   cái gì giấu sau tab, và **thứ tự nút điều hướng** (theo tần suất dùng). Ghi lại **pain point** đời máy trước.
3. **Sketch giấy trước**, dựng ≥ vài phương án để so.
4. **Tránh quá tải màn** (lỗi phổ biến nhất): đẩy thông tin phụ ra tab, phóng to thành phần chính, tận dụng khoảng trắng.
5. **Ngăn lỗi** (disable nút sẽ gây lỗi, xác nhận thao tác hậu quả lớn) thay vì chỉ báo lỗi; **báo lỗi kèm cách xử lý**; trợ giúp tại chỗ.
6. **Component/template tái dùng** nhất quán (khớp Prism Module + UserControl/DataTemplate) — đã có `AM.UI.*` + module pattern.
7. Test mẫu thử với **người dùng thật**, tốt nhất tại hiện trường.

---

## 6. SEMI E95 full compliance — khi nào cần

Chỉ theo đủ **bố cục 4-panel + HCI Compliance Statement** (đánh dấu từng yêu cầu Implemented/Compliant) **khi bán máy cho
nhà máy bán dẫn yêu cầu**. Khi đó: Title/Information/Command(phải)/Navigation(đáy ≤10 nút, Alarm kế cuối + cách rộng, Help cuối),
salience đầy đủ, dialog đúng quy ước, không minimize/close màn chính. Còn lại → **ISA-101 layout hiện tại là đủ và đúng chuẩn**.
