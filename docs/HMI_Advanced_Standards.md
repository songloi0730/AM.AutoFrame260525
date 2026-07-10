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

---

## 7. Đối chiếu bộ UX guidelines web/mobile "RefUX-A" — adoption (S87)

> Nguồn: bộ ~99 UX guidelines + 67 UI styles + 161 palettes cho web/mobile (bí danh **RefUX-A** — xem
> `docs/private/alias.local.md`). Chủ dự án yêu cầu rà xem áp được gì. Kết luận: **KHÔNG cài làm skill**
> (styles/palette/reasoning của nó viết cho marketing web — trigger tự động sẽ đề xuất Glassmorphism/gradient/
> font-pairing ngược ISA-101, gây hại); **chắt lọc nhóm interaction/feedback platform-agnostic** dưới đây.

### 7a. KHÔNG áp (ghi rõ để phiên sau không bê nhầm)

| Nhóm RefUX-A | Lý do không áp |
|---|---|
| 67 UI styles (Glassmorphism, Claymorphism, Neumorphism...) | Ngược High-Performance HMI: nền yên tĩnh, phẳng, không hiệu ứng trang trí. Palette v2 là bảng màu DUY NHẤT. |
| 161 color palettes + 57 font pairings | Màu/typography đã cố định theo ISA-101 (skill am-hmi-design). |
| Mobile-first, breakpoints, viewport, PWA/SEO/performance web | WPF desktop, một cỡ màn 1920×1080 cố định. |
| ARIA/semantic HTML, skip links | Web-specific; WPF dùng AutomationProperties khi cần (chưa là yêu cầu). |
| AI interaction / Spatial UI / Sustainability | Ngoài phạm vi HMI máy đơn. |

### 7b. ÁP — quy tắc interaction/feedback định lượng (bổ sung vào checklist skill)

| Quy tắc | Nội dung áp cho AM.AutoFrame |
|---|---|
| **Feedback ≤ hành động** | Lệnh chạy > **300ms** phải hiện trạng thái bận (IsBusy/spinner/label đổi); KHÔNG để UI "đơ câm". Thao tác xong phải có xác nhận nhìn thấy (status text/toast) — không im lặng cả khi thành công lẫn thất bại. |
| **Chống double-fire** | Nút lệnh async phải **disable trong lúc lệnh đang chạy** (pattern IsBusy đã dùng ở Calib/Backup — nâng thành luật mọi nút lệnh). |
| **Đủ bộ trạng thái nút** | Mỗi nút: enabled / pressed (phản hồi khi bấm) / disabled **kèm LÝ DO** (tooltip/blockReason — pattern guard hiện có). Disabled phải khác enabled rõ rệt (opacity + cursor). |
| **Animation có kỷ luật** | Micro-interaction **150–300ms, ease-out**; KHÔNG animation liên tục trừ chỉ báo bận; KHÔNG animation trang trí (khớp "yên tĩnh khi bình thường"). Tôn trọng cấu hình giảm chuyển động của OS nếu có dùng animation. |
| **Thông điệp tạm vs phải-ack** | Thông báo thành công/tiện ích: tự tắt **3–5s**. Cảnh báo/lỗi cần người xử lý: KHÔNG tự tắt — đi đường alarm/ACK (EEMUA 201 đã có). Không dùng toast cho việc cần ack. |
| **Không layout shift bất ngờ** | Nội dung async phải có chỗ chờ (reserve space/skeleton); ngoại lệ CHỦ ĐÍCH: banner alarm co giãn 36→52px (spec v3). |
| **Truncation có đường xem đủ** | Chữ bị cắt (bảng audit/log/recipe) → ellipsis + tooltip hoặc mở rộng; không cắt cụt không dấu hiệu. |
| **Form nhập liệu** | Label luôn hiển thị (không chỉ watermark/placeholder); lỗi validate hiện **cạnh ô sai** ngay khi rời ô, không dồn hết lên đầu form; ô bắt buộc có dấu hiệu. |
| **Số & ngày** | Số lớn có ngăn cách nghìn theo culture; ngày giờ MỘT định dạng thống nhất `HH:mm:ss dd/MM/yyyy` (đã dùng — nâng thành luật); không hiện số thô kiểu `1234567`. |
| **Empty state có lối đi** | Màn/danh sách rỗng phải nói *vì sao rỗng + làm gì tiếp* (pattern đã dùng: Calib.Empty, Dash.EmptyHint, Backup.Empty — nâng thành luật). |
| **Hành động không đảo ngược** | Bắt buộc xác nhận 2 bước (pattern Override/Restore); nút nguy hiểm không đặt cạnh nút thường (< 48px). |
