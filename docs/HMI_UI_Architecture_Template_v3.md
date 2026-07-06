# HMI_UI_Architecture_Template v3.0 — Persistent Frame 4 vùng (IPC 1920×1080)

> **CHUẨN HIỆN HÀNH** cho shell AM.AutoFrame từ Session 73 (ADR 0009) + tinh chỉnh S74/S78 (ADR 0010, banner prompt).
> Bản này THAY THẾ phần **bố cục shell 7 vùng** của `HMI_UI_Architecture_Template_v2.md`.
> Các phần của v2 **CÒN HIỆU LỰC** (không lặp lại ở đây): §3.4/§3.5 work area + right rail Home,
> §5 palette v2 (token màu giữ nguyên), §7 config schemas, quy tắc multi-alarm EEMUA 201.
> Hiện thực: `AM.Application.Shell/MainWindow.xaml` + `ShellViewModel.cs`.

---

## 1. Bố cục 4 vùng (chrome dọc 168px — nội dung ~912px ≈ 84% @1080p)

```
[1] Header + Nav gộp   56px   logo(tooltip tên máy) · chip AUTO/DRY · chip LOCAL · chip state ISA-88
                              │ tab điều hướng (RadioButton, gạch chân 3px, ScrollViewer ngang)
                              │ recipe · clock(MinWidth chống xô)+heartbeat · ngôn ngữ · user
[2] Alarm banner       36px co giãn → 52px khi có alarm chưa ACK / operator prompt
[3] Content            *      Home tự chia work area + right rail 560px (v2 §3.4/§3.5 + ADR 0010)
[4] Action bar         76px   lệnh máy 64px (Init·Start·Pause/Resume·Stop │ Reset — có divider chống
                              bấm nhầm) · Dry run/Manual 48px · chip "● Thiết bị n/m · Host n/m"
                              (44px, mở Popup 2 cột chi tiết + version ở footer)
```

So với v2 (7 vùng, chrome 284px): header+nav gộp một hàng; connection bar 40px bỏ hẳn (thay bằng chip
+ popup); banner co giãn thay banner cố định 48px. Nút ACK vẫn ≥40px (spec §1.8).

## 2. Kích thước chạm (Master Index §2.9 — giữ nguyên)

| Thành phần | Cao |
|---|---|
| Lệnh máy (Init/Start/Pause/Stop/Reset) | **64px** |
| Nút thường (Dry run, quick action, prompt) | ≥48px (prompt banner 40px — cùng quy tắc ACK) |
| Nút header / chip kết nối | 44px |
| Tab điều hướng | 56px (hết chiều cao header) |
| Chip trạng thái header | 26px (hiển thị, không phải nút) |

## 3. Alarm banner + Operator prompt (mới ở v3)

- **Sạch**: 36px xám (`Notice.OkBrush`), glyph ✓ + ghi chú điều hướng mờ.
- **Alarm chưa ACK**: 52px, nền Warn/Ng theo mức; CHỈ 1 alarm ưu tiên cao nhất + chip `+N` + nút ACK 40px
  (EEMUA 201 — như v2). Ghi chú điều hướng tự ẩn nhường chỗ nội dung.
- **Operator prompt** (sequence engine `OperatorPromptRequired` — S78): 52px nền Warn, text
  "`Chờ operator xử lý — {station} · {message}`" + 3 nút **Thử lại · Bỏ qua · Dừng máy**.
  "Bỏ qua" CHỈ hiện với Engineer+ (UI lọc `Choices` theo quyền — engine gửi đủ, UI cắt).
  Engine chờ `Respond()` trong EventArgs — KHÔNG popup chặn thread (ADR 0011 §4.3).
- Prompt và alarm dùng chung hàng banner; ưu tiên hiển thị: prompt đang chờ > alarm chưa ACK
  (prompt chặn dây chuyền, alarm đã có chip +N giữ chỗ).

## 4. Kiosk mode (mới ở v3 — ADR 0009)

- KHÔNG hardcode trong XAML. Bật qua `appsettings AutoMachine:KioskMode` (mặc định false — dev không bị nhốt).
- Bật: `WindowStyle=None` + `NoResize` + re-maximize che taskbar. **Ctrl+Shift+F11** (Engineer+, audit log)
  vào/thoát lúc chạy; nút trong màn Cài đặt bổ sung ở roadmap P4.3.

## 5. Ba nguyên tắc nội dung (ADR 0010 — áp cho MỌI màn hình mới)

1. **Màu chỉ xuất hiện khi có ý nghĩa trạng thái** — Lỗi=0 hiển thị xám trung tính, KQ OK/NG mới có màu,
   thiết bị bình thường = chấm xanh nhỏ chứ không chữ xanh.
2. **Mọi vùng trống phải nói cho operator bước tiếp theo** — empty state có hướng dẫn
   ("Chưa có sản phẩm trong ca — Khởi tạo → Chạy để bắt đầu"), không để vùng trắng câm.
3. **Thông tin xếp theo tần suất liếc nhìn** — KQ gần nhất > KPI ca > thao tác > log.

## 6. Đối chiếu nhanh v2 → v3

| v2 (7 vùng) | v3 (4 vùng) |
|---|---|
| Header 64 + Nav 48 | Header+Nav gộp 56 (tab RadioButton trong header) |
| Banner 48 cố định | Banner Auto 36→52 co giãn (+ operator prompt) |
| Action bar 84 icon-trên | Action bar 76, nút nằm ngang icon-trái, lệnh máy 64px, divider trước Reset |
| Connection bar 40 | Chip "Thiết bị n/m · Host n/m" + Popup 2 cột (1 `ConnItemTemplate` dùng chung) |
| Cửa sổ thường | Kiosk config-driven + thoát Ctrl+Shift+F11 |
| — | 3 nguyên tắc nội dung (§5) |

Phần template điều khiển (AxisControlView/VisionTeachView) vẫn theo v1.1; work area + right rail Home,
palette, config schemas vẫn theo v2 — xem Master Index §1.

---

*v3.0 — Session 79/P0.4 (04/07/2026). Nguồn quyết định: ADR 0009 (shell), ADR 0010 (nội dung Home), S78 (prompt banner).*
