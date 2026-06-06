# AutoMachine — Prompt Templates
# Copy prompt cần dùng, điền [PLACEHOLDER], paste vào AI

---

## PT-01: Tạo Hardware Interface + Driver + Simulator

```
Tạo hardware driver hoàn chỉnh cho [TÊN THIẾT BỊ].

Thông tin thiết bị:
- Loại: [motion/camera/io/communication]
- SDK/Thư viện: [tên thư viện, namespace]
- Kết nối: [GigE/USB/PCI/TCP/Serial]
- Chức năng chính: [liệt kê 3-5 chức năng]

Yêu cầu đầu ra:
1. Interface I[TênThiếtBị].cs — đặt trong AM.Core.Abstractions/Interfaces/Hardware/
2. Implementation [TênThiếtBị].cs — đặt trong AM.Hardware.[Category]/
3. SimulatedXxx.cs — simulator không cần HW thật, đặt cùng folder
4. Alarm codes mới trong AlarmCodes.cs (range [Nxxx])
5. Unit test cơ bản cho simulator

Alarm codes cần:
- [CATEGORY]_CONNECTION_FAILED = [Nxxx]
- [CATEGORY]_TIMEOUT = [Nxxx+1]
- [CATEGORY]_[ERROR_TYPE] = [Nxxx+2]

Tuân thủ rules:
- Mọi method async + CancellationToken
- Timeout wrapper trên mọi SDK call
- Dispose() giải phóng handle
- Log Debug mỗi command, Error khi lỗi
```

---

## PT-02: Tạo Machine Step

```
Tạo Step cho máy [TÊN MÁY].

Tên step: Step[NN]_[TênBước]
Mục đích: [Mô tả bước này làm gì]

Dependencies cần inject (chỉ dùng interface):
- [IMotionController / ICameraDevice / IIoModule / IAlarmService / ...]

Quy trình thực hiện:
1. [Bước con 1 — e.g., Di chuyển axis X đến vị trí pickup]
2. [Bước con 2 — e.g., Kẹp phôi (DO_CLAMP = ON)]
3. [Bước con 3 — e.g., Kiểm tra kẹp thành công (DI_CLAMP_CONFIRM)]
4. [...]

Điều kiện lỗi và alarm:
- [Điều kiện 1] → Alarm [CODE]: [MESSAGE]
- [Điều kiện 2] → Alarm [CODE]: [MESSAGE]

Timeout: [N]ms cho toàn bộ step
Recipe parameters dùng: [PARAM_1, PARAM_2, ...]

Tuân thủ rules:
- Atomic: thành công hoàn toàn hoặc AlarmException
- Idempotent: chạy lại sau alarm reset an toàn
- Không magic numbers — dùng Recipe properties
```

---

## PT-03: Tạo Service + Interface + Test

```
Tạo service hoàn chỉnh: [TÊN SERVICE]

Mục đích: [Mô tả service này làm gì]

Interface I[Tên]Service — public methods:
- [Method1]: [Mô tả, input, output]
- [Method2]: [Mô tả, input, output]
- [Method3]: [...]

Dependencies:
- [I[Dep]Repository] — data access
- [ILogger<[Tên]Service>] — logging
- [IEventAggregator?] — publish events (nếu cần)

Business rules:
- [Rule 1: e.g., Không thể load recipe khi machine đang Running]
- [Rule 2: e.g., Recipe name phải unique]
- [Rule 3: ...]

Events cần publish (Prism EventAggregator):
- [EventName] khi [điều kiện]

Đầu ra cần:
1. I[Tên]Service.cs trong AM.Core.Abstractions/Services/
2. [Tên]Service.cs trong AM.Services/
3. [Tên]ServiceTests.cs — coverage ≥ 80%
4. DI registration trong Bootstrapper comment
```

---

## PT-04: Tạo WPF Screen (View + ViewModel)

```
Tạo màn hình WPF: [TÊN MÀN HÌNH]

Level: [1=Overview / 2=Workstation / 3=Detail / 4=Engineering]
Navigation path: [Menu > SubMenu > Screen]
User permission cần: [Operator/Technician/Engineer/Admin]

Layout chính:
- [Vùng 1: e.g., DataGrid hiển thị alarm list — 70% chiều cao]
- [Vùng 2: e.g., Filter panel — collapse được]
- [Vùng 3: e.g., Action buttons — bottom]

Data hiển thị (live binding):
- [Property1]: [Kiểu, nguồn từ service nào]
- [Property2]: [...]

Commands:
- [Command1]: [Mô tả hành động, cần permission gì, confirmation?]
- [Command2]: [...]

Localization keys cần (format: Screen.Section.Element):
- [ModuleName].[ScreenName].Title
- [ModuleName].[ScreenName].[ElementName]

Tuân thủ HMI Design Rules:
- Semantic colors qua DynamicResource
- Strings qua lang:Text
- Dangerous action buttons: màu đỏ, confirm dialog
- Live values: Bold, size +2pt so với label
```

---

## PT-05: Viết Unit Test cho class có sẵn

```
Viết unit test đầy đủ cho class: [CLASS_NAME]

File đang test: [path/to/ClassName.cs]

Dependencies cần mock:
- [IDependency1] — Mock<IDependency1>
- [IDependency2] — Mock<IDependency2>

Test cases cần bao gồm:

Happy path:
- [Scenario 1: e.g., Load recipe thành công → recipe được cache]
- [Scenario 2: e.g., Raise alarm → alarm xuất hiện trong active list]

Edge cases:
- [Null/empty input]
- [Giá trị biên (0, max, negative)]
- [Duplicate action (raise alarm đã tồn tại)]

Error paths:
- [Repository throw exception → service handle đúng]
- [Hardware timeout → AlarmException đúng code]
- [CancellationToken cancelled → OperationCanceledException]

Verify interactions:
- [Repository được gọi đúng số lần]
- [Event được publish khi cần]
- [Audit log được gọi cho dangerous actions]

Framework: xUnit + Moq + FluentAssertions
Convention tên: {Method}_{Condition}_{ExpectedResult}
```

---

## PT-06: Code Review

```
Review code sau đây theo AutoMachine coding standards.

[DÁN CODE VÀO ĐÂY]

Kiểm tra và báo cáo:
1. Vi phạm Architecture Rules (layer dependency, interface usage)
2. Async/await issues (missing CT, blocking calls, thread safety)
3. Exception handling (missing catch, swallowed exceptions)
4. Logging (level đúng không, có structured data không)
5. Security (hardcode credential, missing permission check)
6. Performance (N+1 query, missing index, memory leak risk)
7. Naming conventions vi phạm
8. Missing null checks

Format báo cáo:
Line {N}: [CRITICAL/WARNING/INFO] {vấn đề} → {cách sửa đề xuất}

Sau đó: Viết lại đoạn code với tất cả vấn đề đã sửa.
```

---

## PT-07: Refactor class lớn

```
Refactor class sau — hiện đang vi phạm Single Responsibility Principle.
Class: [CLASS_NAME] — [N] dòng

[DÁN CODE VÀO ĐÂY]

Yêu cầu:
1. Phân tích: class này đang làm bao nhiêu việc khác nhau?
2. Đề xuất tách thành các class nhỏ hơn
3. Tạo interface cho class mới nếu cần
4. Viết lại với các class đã tách
5. Đảm bảo external behavior không đổi
6. Liệt kê test cases cần check sau refactor

Constraint:
- Không thay đổi public interface của class gốc (nếu có class khác dùng)
- Mỗi class mới: ≤ 150 dòng, 1 mục đích rõ ràng
```

---

## PT-08: Tạo Alarm Dictionary entry

```
Thêm alarm mới vào hệ thống:

Alarm Code: [NXXXX] (range: [tên nhóm])
Name: [ALARM_CODE_NAME trong AlarmCodes.cs]
Level: [Critical/High/Medium/Low]
Station: [tên station hoặc "ALL"]

Message (vi-VN): [Mô tả lỗi bằng tiếng Việt — rõ ràng, có nguyên nhân]
Message (en-US): [English version]
Message (zh-CN): [Chinese version — để trống nếu chưa có]

Cause: [Nguyên nhân gây ra alarm này]
Action: [Hướng dẫn operator xử lý — cụ thể từng bước]

Trigger condition: [Khi nào alarm này được raise]
Auto-clear condition: [Khi nào alarm tự clear / cần manual clear]

Đầu ra:
1. Thêm const vào AlarmCodes.cs
2. Thêm entry vào alarm-dictionary.xml (vi-VN + en-US)
3. Code AlarmException throw ở đúng chỗ trong sequence
```

---

## PT-09: Database Migration

```
Tạo EF Core migration cho thay đổi sau:

Thay đổi: [Mô tả thay đổi schema]

Entity cần thêm/sửa:
- [EntityName]
  - Thêm field: [FieldName] : [Type] [nullable?]
  - Sửa field: [FieldName] từ [OldType] sang [NewType]
  - Xoá field: [FieldName]

Index cần thêm: [column(s)]
Default value cho existing rows: [value hoặc NULL]

Đầu ra:
1. Cập nhật Entity class
2. Cập nhật DbContext (Fluent API)
3. Migration file với Up() và Down()
4. Seed data nếu cần

Lưu ý: Database đang production tại khách hàng
→ Migration phải backward compatible
→ Không DROP column có data (chỉ add/rename)
```

---

## PT-10: Tạo Report Export

```
Tạo tính năng xuất báo cáo: [TÊN BÁO CÁO]

Nguồn dữ liệu: [Service và method lấy data]
Format xuất: [Excel / CSV / PDF]
Thư viện: [NPOI cho Excel / CsvHelper cho CSV]

Cột dữ liệu:
| Tên cột | Property | Format | Width |
|---------|---------|--------|-------|
| [Tên]   | [Prop]  | [fmt]  | [px]  |

Filter:
- Date range: [từ ngày — đến ngày]
- [Filter khác nếu có]

Styling (Excel):
- Header: bold, background [màu từ theme]
- Alternating rows: [màu xen kẽ]
- Freeze header row

Export location: [ProgramData/AutoMachine/Reports/{date}_{name}.xlsx]
Show save dialog: [yes/no]
Open after export: [yes/no]

Đầu ra:
1. IReportExporter interface (nếu chưa có)
2. [Name]ReportExporter.cs
3. ExportCommand trong ViewModel
4. Progress dialog khi xuất (nếu > 1000 rows)
```

---

## PT-11: Quick Fix — Lỗi thường gặp

### Fix memory leak (event handler):
```
Class sau bị memory leak vì subscribe event không unsubscribe.
[DÁN CODE]
Sửa: thêm IDisposable, unsubscribe tất cả event trong Dispose().
Giữ nguyên behavior, chỉ thêm cleanup.
```

### Fix UI freeze:
```
Method sau đang block UI thread:
[DÁN CODE]
Sửa: convert sang async/await, đảm bảo không block UI thread.
Thêm loading indicator nếu operation > 500ms.
```

### Fix missing null check:
```
Thêm null checks cho tất cả tham số constructor và public method parameters.
[DÁN CODE]
Dùng: ArgumentNullException.ThrowIfNull() (.NET 6+)
```

### Fix hardcoded values:
```
Extract tất cả magic numbers và strings thành named constants hoặc config.
[DÁN CODE]
Constants đặt ở đâu phù hợp nhất trong project structure?
```
