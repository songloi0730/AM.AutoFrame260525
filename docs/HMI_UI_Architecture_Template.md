# HMI/UI Architecture Template — Khung giao diện chung cho máy tự động hóa

> **Mục đích:** Tài liệu mẫu (master reference) để dùng kết hợp với Claude khi thiết kế / sinh giao diện cho **nhiều máy tự động hóa khác nhau**, sao cho khi đổi máy chỉ phải thay đổi tối thiểu (config + recipe + lớp vendor HAL), khung giao diện giữ nguyên.
>
> **Chuẩn áp dụng:** ISA-101 (HMI Design), SEMI E95 (Operator Interface), PackML/ISA-88 (state model).
> **Stack mục tiêu:** WPF · .NET 9 · Prism + DryIoc · EF Core + SQLite.

---

## 0. Cách dùng tài liệu này với Claude

Khi muốn Claude tạo/sửa một màn hình, dán kèm tài liệu này và nêu rõ:

1. **Máy nào / station nào** (tên, loại unit).
2. **Thuộc Level mấy** (L1–L4) và **Group Menu nào** (1–4).
3. **Template tái sử dụng nào** áp dụng (xem §6).
4. **Phần thay đổi theo máy** (danh sách item/IO/recipe) — đưa dưới dạng config, KHÔNG yêu cầu hardcode.

> Nguyên tắc vàng yêu cầu Claude luôn tuân thủ:
> **Lớp logic máy (Station / Mechanism / Sequence) KHÔNG được tham chiếu kiểu vendor.**
> Đổi vendor = chỉ đổi 1 project HAL + 1 file config.

---

## 1. Information Architecture (IA) — Phân tầng màn hình

ISA-101 đề xuất mô hình **4 cấp Display Hierarchy**, kết hợp SEMI E95 (phân quyền vận hành):

| Level | Tên (ISA-101)    | Mục đích                                              | Mức tái sử dụng |
|-------|------------------|------------------------------------------------------|-----------------|
| **L1** | Process Overview | Tổng quan toàn máy/dây chuyền, KPI, trạng thái PackML | Cố định, chỉ đổi data binding |
| **L2** | Process Control  | Điều khiển từng station/unit, vận hành chính          | Template theo loại unit |
| **L3** | Process Detail   | Chi tiết thiết bị (motion, IO, vision, sensor)        | Component-based, tái dùng cao |
| **L4** | Diagnostic/Support | Trend, alarm history, manual, troubleshoot          | Dùng chung 100% |

**Quy tắc điều hướng:** tối đa 2 cấp drill-down từ Home (tránh sâu > 3 cấp).

---

## 2. Page Hierarchy — Cây phân cấp màn hình

```
ROOT (Shell – luôn cố định)
│
├── L1: HOME / OVERVIEW
│     ├── Machine status (PackML state)
│     ├── Production KPI (OEE, count, yield, UPH)
│     └── Station summary tiles → link L2
│
├── L2: OPERATION (Production Control)
│     ├── Auto Mode (sequence run/pause/stop)
│     ├── Manual / Jog Mode  → ManualControlView
│     ├── Recipe / Parameter
│     └── Station Control (1..n) → link L3
│
├── L3: EQUIPMENT DETAIL
│     ├── Motion (axis control, position)
│     ├── IO Monitor (DI/DO/AI/AO)
│     ├── Vision (camera, result) → VisionTeachView
│     └── Mechanism / Tooling
│
├── L4: SUPPORT
│     ├── Alarm (active + history) → ErrorDetailView
│     ├── Trend / Data Logging
│     ├── Diagnostics / Maintenance
│     ├── Login / Account (đăng nhập người dùng)
│     └── Settings / About / Language
│
└── PERSISTENT FRAME (mọi màn hình – xem §3)
```

### Cây Group Menu (đặt tên 4 module gốc)

| Group | Module gốc        | Mục con tiêu biểu                                                  | Level |
|-------|-------------------|--------------------------------------------------------------------|-------|
| **1** | `SetupModule`     | Device Setup · Motor/Axis Setup · Parameter · Teaching · Manual ten-key | L2/L3 |
| **2** | `MaintModule`     | Sensor/IO Monitor · Life Time · Repeatability · PC Monitor · Abrasion | L3/L4 |
| **3** | `HistoryModule`   | Vision Log · Lot Tracking · MTBA/MTBF · Comm Log · Error Setup      | L4    |
| **4** | `ManagementModule`| Lamp Setup · **Language** · (Login/Account)                       | L4    |

> Mỗi máy bật/tắt mục con qua **config catalog** (Prism ModuleCatalog đọc từ file), không sửa code.

---

## 3. Persistent Frame — Khung cố định (chìa khóa tái sử dụng)

Phần KHÔNG đổi giữa các máy. Bố cục Auto Screen điển hình:

```
┌─ COMMAND BAR ───────────────────────────────────────────────┐
│ [Recipe name field]                  [Smart Utility][Vacuum] │  ← quick tools góc phải
├─ NOTICE / ERROR BAR ─────────────────────────────────────────┤  ← lỗi ưu tiên cao nhất, click → chi tiết
├─ TAB BAR: [Status][H-Compensation][Map Info] ───────────────┤  ← tab nội dung (tùy màn hình)
│                                                               │
│   CONTENT AREA  (machine layout động — phần đổi theo máy)     │
│                                                               │
├─ INFO BAR: datetime | Recipe name | Trace | 🌐Language | Ver ─┤
└─ ACTION BAR ─────────────────────────────────────────────────┤
   [Logo][Main][Menu][A/S] │ [Run][Reset][Dry][Inspect] │ [Quick][Switch][Login]
```

**Ba cụm cố định của Action Bar:**

| Cụm          | Nút                          | Vai trò |
|--------------|------------------------------|---------|
| Điều hướng   | Main · Menu · A/S            | Về Home, mở menu, gửi snapshot trạng thái máy (hỗ trợ kỹ thuật) |
| Vận hành     | Run · Reset · Dry · Inspect  | Lệnh chạy/điều khiển chu trình |
| Tiện ích/User| Quick · Switch · Login       | Lệnh nhanh, chuyển chế độ, đăng nhập người dùng |

**Quy tắc bố cục ISA-101:** Header / Error Bar / Info Bar / Action Bar **luôn cố định vị trí** trên mọi màn hình — vận hành viên không bao giờ phải "tìm" thông tin.

### 3.1 Biến thể Main Menu dạng lưới (Grid Menu)

Ngoài thanh menu dọc, có thể dùng **menu chính dạng lưới thẻ nhóm** — gom toàn bộ chức năng vào một màn hình duy nhất, mỗi thẻ = một nhóm chức năng, bên trong là danh sách mục con kèm đèn trạng thái. Bố cục này phủ kín màn hình, ít thao tác điều hướng, phù hợp HMI cảm ứng.

```
┌─ Header: [Logo][Machine model] [DISABLED][REMOTE][PAUSE] ... [icons] ─┐
├──────────────┬──────────────┬──────────────┬──────────────┬──────────┤
│ Device Data  │ Conversion   │ Teaching     │ Basic Setup  │  MAIN    │
│ • Management │ Setup        │ • Step A     │ • Item 1     │  MENU    │
│ • Modify     │ • Item ...   │ • Step B     │ • Item 2     │ (cột nút │
│ • Recipe     │ • Item ...   │ • ...        │ • ...        │  dọc)    │
├──────────────┼──────────────┼──────────────┼──────────────┤          │
│ Engineering  │ Logging      │ System Health│ (Group ...)  │ • Group1 │
│ • Lamp/Buzzer│ • MTBF       │ • Motor Check│              │ • Group2 │
│ • Life-Time  │ • Tracking   │ • IO Check   │              │ • ...    │
│ • Account    │ • Log View   │ • ...        │              │          │
├──────────────┴──────────────┴──────────────┴──────────────┴──────────┤
│ ACTION BAR: [Menu][Summary][Home][Run][Stop][Reset][Dry] ... [Quick]  │
├───────────────────────────────────────────────────────────────────────┤
│ Trace [Timestamp]      HMI: ...      S/N: ...     Firmware: V1.0       │
└───────────────────────────────────────────────────────────────────────┘
```

Đặc điểm đáng áp dụng:
- **Mỗi thẻ nhóm có thanh tiêu đề màu riêng** giúp phân biệt nhanh nhóm chức năng (vẫn theo nguyên tắc ISA-101: màu để định danh nhóm, không lạm dụng cho nền).
- **Đèn trạng thái nhỏ trước mỗi mục con** cho biết mục đã cấu hình / đang hoạt động / lỗi.
- Toàn bộ thẻ + mục con **nạp từ config catalog** → đổi máy chỉ đổi danh sách, layout lưới giữ nguyên (dùng `ItemsControl` + `UniformGrid`).

> Hai biến thể (thanh dọc ở §3 và lưới ở §3.1) dùng chung dữ liệu menu; chọn biến thể qua cấu hình.

---

## 4. Navigation Flow & Screen Flow

### 4.1 Navigation Flow — Luồng điều hướng

```
        ┌──────────┐
        │  HOME    │ ◄──── nút Home/Main luôn có ở Action Bar
        │  (L1)    │
        └────┬─────┘
             │ chọn station / chức năng
     ┌───────┼────────┬──────────┬──────────┐
     ▼       ▼        ▼          ▼          ▼
 OPERATION RECIPE  ALARM     SETTINGS   LANGUAGE
   (L2)     (L2)   (L4)        (L4)       (L4)
     │
     │ chọn thiết bị
     ▼
 EQUIPMENT DETAIL (L3)
     │
     ▼
 DIAGNOSTIC / ERROR DETAIL (L4)

Quy tắc:
- Tối đa 2 cấp drill-down từ Home.
- Back / Home truy cập được từ mọi nơi (Action Bar).
- Error Bar → click → nhảy thẳng tới ErrorDetailView (cause/fix/location).
```

### 4.2 Screen Flow — Luồng sử dụng theo tác vụ

```
Vận hành cơ bản : Home → Operation → Auto Run → Alarm Ack
Chỉnh/bảo trì   : Manual/Jog → Sensor-IO Monitor → Motion/Axis tuning → Diagnostics
Cấu hình        : Recipe edit → Parameter → Lamp Setup → Config
```

**Về đăng nhập:** giao diện có **chức năng đăng nhập người dùng** (nút Login trên Action Bar) để mở phiên làm việc. Cơ chế phân quyền / mật khẩu chi tiết sẽ bổ sung sau — phần này hiện chỉ cần chỗ đặt và luồng đăng nhập/đăng xuất cơ bản.

---

## 5. Color & Status — Chuẩn hóa ISA-101

| Priority    | Màu                       | Dùng cho                    |
|-------------|---------------------------|-----------------------------|
| Critical    | Đỏ                        | Alarm dừng máy              |
| High        | Cam / Vàng                | Cảnh báo cần xử lý ngay      |
| Low         | Vàng nhạt                 | Thông báo                   |
| Normal / OK | Nền xám trung tính, xanh nhạt | Trạng thái bình thường  |

**Nguyên tắc ISA-101:** nền xám trung tính, **màu chỉ dùng cho bất thường** — không dùng màu trang trí làm loãng cảnh báo.

### Status Lamp (đèn tháp ảo, config-driven)

| PackML State | Màu mặc định | Ghi chú             |
|--------------|--------------|---------------------|
| RUN          | Xanh dương   | Máy đang chạy        |
| STOP         | Đỏ           | Máy dừng             |
| RESET        | Vàng         | Có lỗi               |
| DRY          | Trắng        | Chạy thử (dry run)   |
| INSPECT      | Không đổi    | Đang kiểm tra unit   |

> Map state→màu nằm trong **config**, không hardcode. Control: `StatusLampControl`.

---

## 6. Reusable Templates — Mẫu màn hình dùng chung

### 6.1 `ManualControlView` — Bảng điều khiển thủ công (data-driven)

Danh sách lệnh nạp từ config/DB; user tự thêm/sửa/xóa item; có khu **Quick** gom lệnh hay dùng.

```
┌────────────────────────────────────────────┐
│ [Quick] ← lệnh thường dùng (user-defined)    │
├────────────────────────────────────────────┤
│ Item list (load từ config/DB):               │
│  • Cylinder A   [On][Off][Blow]               │
│  • Motor X      [Jog+][Jog−][value:___][Go]   │
│  • Vacuum 1     [On][Off]  ●sensor            │
│  [+ Add] [Edit] [Delete]                      │
└────────────────────────────────────────────┘
```
> Đổi máy = đổi danh sách item (config). XAML không đổi. Bind theo `IHardwareDevice`/`IStation`.

### 6.2 `AxisControlView` — Điều khiển trục / động cơ (motion, dùng chung mọi trục)

Màn hình chi tiết cho **một trục servo/động cơ**, tái dùng cho mọi trục (X/Y/Z/θ…) chỉ bằng cách bind sang `IAxisDevice` khác. Gom đủ thành phần điều khiển motion vào các vùng cố định:

```
┌─ Header: [icon] Tên trục (vd: "OCR Module X")  Loại: Axis | Sim: True   Comm: ●True ─┐
├──────────────────────────────┬───────────────────────────────────────────────────────┤
│ REAL-TIME POSITION (DRO)      │  ┌ Absolute target [um][___]  Speed[___]  [Absolute move]│
│   ┌──────────────────────┐    │  └ Relative dist  [um][___]  Speed[___]  [Relative move]│
│   │     0.000   um        │    │  ┌ [◀ JOG−]   Jog speed [___ um/s]        [JOG+ ▶]      │
│   └──────────────────────┘    │  └──────────────────────────────────────────────────── │
│                                │  ┌ PRESET POINT TABLE (tách tọa độ khỏi code)           │
│ IO STATUS MONITOR             │  │  N │ Point name │ Target │ Speed │ Acc │ Dec │ S-crv  │
│  □ Servo ON (SVON)            │  │  1 │ Home pos   │  0     │  0    │  0  │  0  │ 0.08 [Move to]│
│  □ Moving(BUSY) □ Homing(HOME)│  │  ...                                                  │
│  □ +Limit(PEL)  □ Origin(ORG) │  └ [Get current pos][Delete selected][Save changes]      │
│  □ −Limit(NEL)  □ Alarm(ALM)  │                                                          │
├──────────────────────────────┤                                                          │
│ DEVICE START/STOP             │                                                          │
│  [Connect HW] [Disconnect]    │                                                          │
│  [Servo ON]   [Servo OFF]     │                                                          │
└──────────────────────────────┴───────────────────────────────────────────────────────┘
```

Thành phần & nguyên tắc:

| Vùng | Nội dung | Ghi chú thiết kế |
|------|----------|------------------|
| **DRO** | Vị trí thời gian thực (đơn vị cấu hình: um/mm/deg) | Số lớn, dễ đọc; đổi màu khi đang Moving |
| **IO Status** | SVON · BUSY · HOME · PEL · NEL · ORG · ALM | Đèn chỉ báo; limit/alarm tô đỏ khi kích hoạt (ISA-101) |
| **Move commands** | Absolute / Relative / Jog (+speed) | Mỗi lệnh kèm ô tốc độ riêng |
| **Device start/stop** | Connect/Disconnect · Servo ON/OFF | Tách kết nối phần cứng khỏi bật servo |
| **Point Table** | Bảng tọa độ preset: name, target, speed, acc, dec, S-curve | **Tách tọa độ quy trình ra khỏi code**; thêm/xóa/sửa, "Get current pos" để dạy nhanh, "Move to" để chạy thử |

> **Vì sao quan trọng:** Point Table tách dữ liệu vị trí khỏi logic → đổi máy/đổi layout cơ khí chỉ sửa bảng tọa độ (lưu DB/config), không sửa code chuyển động. Đúng nguyên tắc vendor-agnostic. Bind toàn bộ theo interface `IAxisDevice` (vị trí, IO bit, lệnh move) → driver vendor nằm ở HAL.
>
> Có thể đặt `AxisControlView` ở L3 (Motion detail) và nhúng nhiều instance vào một màn hình tổng quan motion nếu cần điều khiển đồng thời nhiều trục.

### 6.3 `VisionTeachView` — Màn hình dạy vision (5 vùng cố định)

Dùng chung cho mọi loại package (QFN, BGA, Mark, Tray…).

```
┌─ a. Camera Selection ──┬─ b. Status Bar (PASS/Teach Complete/Error) ─┐
├────────────────────────┴──────────────────────────┬─ c. Mode Select ─┤
│                                                     │                  │
│   d. IMAGE VIEW PANE (live/snap + ROI overlays)     │   e. TOOL BOX    │
│                                                     │  [Zoom][Overlay] │
│                                                     │  [Snap][Live]    │
│                                                     │  [Threshold]     │
│                                                     │  [ErrorImg][Debug]│
│                                                     │  [Gray][Device]  │
└─────────────────────────────────────────────────────┴──────────────────┘
```
> Layout cố định; bộ icon Tool Box thay đổi theo loại camera/model qua `IToolBoxProvider`.

### 6.4 `SensorVacuumMonitor` — Giám sát cảm biến/chân không

Gauge + số thực + đèn On/Off; toggle Setting/Monitor; ngưỡng High/Low/Off-High/Off-Low + delay time.

### 6.5 `ErrorDetailView` — Chi tiết lỗi (cause / remedy / location)

Click error trên Notice Bar → hiển thị toàn màn hình: nguyên nhân, cách xử lý, vị trí lỗi kèm hình; tab Detail cho phép thao tác I/O, motor, ten-key liên quan.

### 6.6 `StatusLampControl` — Đèn trạng thái map PackML→màu (config).

---

## 7. Language Switching — Đổi ngôn ngữ ở giao diện chính

> Yêu cầu: nút 🌐 **Language** đặt ngay trên **Info Bar** của Persistent Frame (luôn hiển thị), cho phép đổi ngôn ngữ tức thì mà không cần khởi động lại.

### 7.1 Nguyên tắc thiết kế

1. **Không hardcode chuỗi** trong XAML/code — mọi text lấy từ resource theo key.
2. **Đổi ngôn ngữ runtime** — toàn bộ UI cập nhật ngay (live), không restart.
3. **Ngôn ngữ mặc định + fallback** — thiếu key ở ngôn ngữ A thì rơi về ngôn ngữ gốc (vd. `en`).
4. **Lưu lựa chọn** — ghi vào config/DB, lần mở sau giữ nguyên ngôn ngữ đã chọn.
5. **Tách biệt theo tầng** — chuỗi UI, tên alarm/error, tên parameter để ở các catalog riêng để đội vận hành/đa quốc gia dễ dịch.
6. **Hỗ trợ tiếng Việt** là ngôn ngữ chính + tối thiểu English; mở rộng thêm (Korean/Chinese…) chỉ bằng cách thêm file resource.

### 7.2 Vị trí & luồng

```
INFO BAR:  ... | 🌐 [ Tiếng Việt ▾ ]  ← click
                     ├─ Tiếng Việt
                     ├─ English
                     └─ (thêm ngôn ngữ = thêm resource file)
        │
        ▼
 Đổi CurrentUICulture  →  raise PropertyChanged toàn cục
        │
        ▼
 Mọi binding {loc:Translate Key} cập nhật tức thì
        │
        ▼
 Lưu lựa chọn vào config (SQLite)  →  áp dụng lại lần mở sau
```

### 7.3 Cấu trúc resource đề xuất

```
/Localization
  ├── Strings.en.json        # UI strings — English (fallback gốc)
  ├── Strings.vi.json        # UI strings — Tiếng Việt
  ├── Strings.ko.json        # (tùy chọn)
  ├── Alarms.en.json         # tên/diễn giải alarm
  ├── Alarms.vi.json
  └── Parameters.{lang}.json # nhãn parameter/recipe
```

Ví dụ cặp key (giữ key giống nhau giữa các file):

```json
// Strings.en.json
{ "btn.run": "Run", "btn.reset": "Reset", "menu.setup": "Setup",
  "lbl.recipe": "Recipe", "msg.confirmStop": "Confirm machine stop?" }
```
```json
// Strings.vi.json
{ "btn.run": "Chạy", "btn.reset": "Đặt lại", "menu.setup": "Thiết lập",
  "lbl.recipe": "Công thức", "msg.confirmStop": "Xác nhận dừng máy?" }
```

### 7.4 Cách dùng trong XAML (WPF)

Một markup-extension `Translate` đọc theo `CurrentUICulture`, tự cập nhật khi đổi:

```xml
<!-- thay vì Content="Run" -->
<Button Content="{loc:Translate btn.run}" />
<TextBlock Text="{loc:Translate lbl.recipe}" />

<!-- ComboBox đổi ngôn ngữ trên Info Bar -->
<ComboBox ItemsSource="{Binding AvailableLanguages}"
          SelectedItem="{Binding CurrentLanguage, Mode=TwoWay}"
          DisplayMemberPath="NativeName" />
```

### 7.5 Service tối thiểu (interface, không hardcode UI)

```csharp
public interface ILocalizationService
{
    CultureInfo Current { get; }
    IReadOnlyList<CultureInfo> Available { get; }   // nạp từ thư mục resource
    event EventHandler LanguageChanged;             // UI nghe để refresh

    string Get(string key);                         // có fallback về ngôn ngữ gốc
    void SetLanguage(CultureInfo culture);          // đổi runtime + lưu config
}
```

> **Lưu ý kỹ thuật:** dùng cơ chế phát `PropertyChanged`/event toàn cục để binding refresh tức thì; tránh phải đóng/mở lại cửa sổ. Đăng ký `ILocalizationService` là **singleton** trong DryIoc để mọi module dùng chung.

### 7.6 Checklist đa ngôn ngữ

- [ ] Không còn chuỗi cứng trong XAML/ViewModel.
- [ ] Mọi alarm/error/parameter đều có key dịch.
- [ ] Có fallback khi thiếu key.
- [ ] Layout chịu được chuỗi dài/ngắn khác nhau (tiếng Việt thường dài hơn English ~20–30%).
- [ ] Lựa chọn ngôn ngữ được lưu & khôi phục.
- [ ] Thêm ngôn ngữ mới = chỉ thêm file resource, không sửa code.

---

## 8. Ánh xạ kỹ thuật vào WPF / Prism (AM.AutoFrame)

| Thành phần kiến trúc        | Hiện thực Prism/WPF |
|-----------------------------|---------------------|
| Persistent Frame            | `Shell.xaml` với region: `CommandBarRegion`, `ErrorBarRegion`, `ContentRegion`, `InfoBarRegion`, `ActionBarRegion` (cố định) |
| Mỗi Level/màn hình          | Một Prism **Module**, đăng ký view vào `ContentRegion` |
| Group Menu (1–4)            | 4 module gốc; mục con nạp động qua ModuleCatalog đọc từ config |
| Template L2/L3              | `UserControl` + `DataTemplate` bind theo `IStation`/`IHardwareDevice` |
| ManualControlView           | ItemsControl bind danh sách lệnh từ DB/config |
| AxisControlView             | UserControl bind theo `IAxisDevice` (DRO, IO bit, lệnh move); Point Table lưu DB/config |
| VisionTeachView             | UserControl 5 vùng + `IToolBoxProvider` theo camera type |
| Đăng nhập                   | `ILoginService` đơn giản (mở/đóng phiên); phân quyền chi tiết bổ sung sau |
| Status Lamp / Error mapping | Bảng config (JSON/SQLite): state→màu, errorCode→remedy/image |
| **Đổi ngôn ngữ**            | `ILocalizationService` (singleton) + markup-extension `Translate` + ComboBox trên Info Bar |
| Vendor HAL                  | Project riêng, chỉ HAL tham chiếu kiểu vendor |

### Phần thay đổi giữa máy (và CHỈ những phần này)

1. **Config** — danh sách module/station, item ten-key, map đèn, map error.
2. **Recipe schema** — tham số riêng của máy.
3. **Lớp vendor HAL** — driver motion/IO/vision/barcode cụ thể.

Mọi thứ còn lại (Shell, Group Menu, templates, localization, đăng nhập) **giữ nguyên** giữa các máy.

---

## 9. Sơ đồ tổng quan (one-page)

```
                         ┌─────────────────────────────────────┐
                         │              SHELL                    │
                         │  (Persistent Frame – cố định 100%)    │
                         │ ┌─ CommandBar (recipe + quick) ─────┐ │
                         │ ├─ ErrorBar (top error → detail) ───┤ │
                         │ │            CONTENT REGION          │ │ ← đổi theo máy
                         │ ├─ InfoBar (datetime|recipe|🌐Lang) ┤ │
                         │ └─ ActionBar (Nav|Run/Reset|User) ──┘ │
                         └───────────────┬───────────────────────┘
                                         │ Prism regions
        ┌────────────────┬───────────────┼───────────────┬────────────────┐
        ▼                ▼               ▼               ▼                ▼
  L1 OVERVIEW      L2 OPERATION    L3 DETAIL        L4 SUPPORT        4 GROUP MENU
  KPI/PackML       Auto/Manual     Motion/Axis/IO   Alarm/Trend       1 Setup
  tiles→L2         Recipe          Vision/Tooling   Diag/Login        2 Maint
                   ↳ManualCtrl     ↳AxisControl     ↳ErrorDetail      3 History
                                   ↳VisionTeach     Language          4 Management

  ── REUSABLE ──   ManualControlView · AxisControlView · VisionTeachView
                   SensorVacuumMonitor · ErrorDetailView · StatusLampControl
  ── SERVICES ──   ILoginService · ILocalizationService · IHardwareDevice · IAxisDevice
  ── CONFIG ──     module catalog · lamp map · error map · point table · recipe schema  (đổi/máy)
  ── HAL ──        vendor driver (chỉ project này biết kiểu vendor)                     (đổi/máy)
```

---

## 10. Prompt mẫu để dùng với Claude

> "Dựa trên `HMI_UI_Architecture_Template.md`, tạo cho tôi màn hình **[tên màn hình]** thuộc **Level [n] / Group Menu [n]**, áp dụng template **[ManualControlView/AxisControlView/VisionTeachView/...]**. Các item/IO/trục/parameter ở dưới đây (config). Yêu cầu: dùng Prism region của Shell hiện có, mọi chuỗi qua `ILocalizationService` (key tiếng Anh + Việt), có chỗ đặt chức năng đăng nhập, KHÔNG tham chiếu kiểu vendor trong View/ViewModel (bind qua interface, driver nằm ở HAL)."

---

*Tài liệu mẫu — phiên bản 1.1. Tham chiếu chuẩn ISA-101, SEMI E95, PackML/ISA-88.*
