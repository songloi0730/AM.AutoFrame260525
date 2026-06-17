# 0001 — Các lựa chọn thiết kế tổng thể của AM.AutoFrame

> Living doc — giải thích **vì sao** dự án được dựng như hiện tại. Mỗi mục: *Quyết định → Phương án khác đã cân nhắc
> → Vì sao chọn → Đánh đổi*. Đọc cùng `CLAUDE.md` (luật) và `.claude/rules/` (chi tiết).

AM.AutoFrame là **framework C#/.NET 9 (WPF + Prism)** cho phần mềm điều khiển máy tự động hoá công nghiệp. Mục tiêu xuyên
suốt: *làm máy mới chỉ viết phần `AM.WorkStation.{Máy}` + config, không sửa lõi/UI*; và *an toàn là bất biến số một*.

---

## 1. Kiến trúc 3 tầng: MasterController → Station → Mechanism
**Quyết định.** Cây máy 3 tầng: `Mechanism` (bọc 1–N thiết bị, lộ method nghiệp vụ `PickAsync`…), `Station` (điều phối
các Mechanism cho một công đoạn, KHÔNG gọi hardware trực tiếp), `MasterController` (nơi DUY NHẤT fire MachineTrigger +
quản state machine + điều phối pipeline).
**Phương án khác.** (a) *Phẳng* — controller gọi thẳng driver: nhanh cho máy nhỏ nhưng rối khi nhiều cụm, khó tái dùng.
(b) *2 tầng* (Controller→Device): thiếu lớp "công đoạn" nên logic trạm trộn vào controller.
**Vì sao chọn.** Tách *điều phối* khỏi *thao tác phần cứng* → mỗi tầng test/đổi độc lập; Mechanism tái dùng giữa máy;
Station ghép lại thành dây chuyền qua `IStationSyncService` (không busy-wait, không gọi chéo nhau).
**Đánh đổi.** Nhiều lớp hơn cho máy đơn giản; phải kỷ luật "Station không chạm hardware".

## 2. Interface-over-implementation + một Composition Root duy nhất
**Quyết định.** Field/param/return luôn là interface; lớp cụ thể chỉ biết ở `AM.Application.Shell/Bootstrapper.cs`.
Tách `AM.Core.Abstractions` (chỉ interface) khỏi mọi implementation.
**Phương án khác.** Tham chiếu trực tiếp lớp cụ thể (new tại chỗ): ít file hơn nhưng buộc cứng vào driver, không thay sim↔real,
khó test.
**Vì sao chọn.** Đổi sim↔thật chỉ sửa 1 chỗ (`appsettings UseSimulation`); WorkStation chỉ tham chiếu Abstractions →
không thể lỡ gọi driver thật; unit test mock interface dễ.
**Đánh đổi.** Nhiều interface "mỏng"; thêm thiết bị phải khai interface trước.

## 3. State machine ISA-88, 8 trạng thái, trigger tập trung
**Quyết định.** 8 trạng thái (Uninitialized→Initializing→Idle→Running→Paused→…+InitAlarm/RunAlarm/Resetting) với bộ
trigger cố định; chỉ MasterController đổi state.
**Phương án khác.** Cờ boolean rời rạc (`isRunning`, `isError`…): dễ rơi vào tổ hợp trạng thái vô lý.
**Vì sao chọn.** ISA-88 là chuẩn ngành; máy state tường minh chặn chuyển trạng thái sai; UI map state→nhãn/đèn nhất quán.
**Đánh đổi.** Phải định nghĩa đủ transition; cứng nhắc hơn cờ tuỳ tiện (đó là điểm mạnh về an toàn).

## 4. Simulation parity — mọi driver có `SimulatedXxx`
**Quyết định.** Mỗi driver phần cứng có bản giả lập chạy được không cần thiết bị; bật/tắt qua `UseSimulation`.
**Phương án khác.** Chỉ test trên máy thật / mock rời rạc trong test: không chạy full app khi chưa có phần cứng.
**Vì sao chọn.** Phát triển + demo + CI không cần phần cứng; UI/luồng kiểm thử end-to-end bằng sim; là "bệ đỡ" cho mọi tính năng.
**Đánh đổi.** Nhân đôi số lớp driver; sim phải bám sát ngữ nghĩa thật (vd force IO ở §S62 phải hành xử như thật).

## 5. UI auto-discovery qua attribute (thay đăng ký tay)
**Quyết định.** `[ModuleNavigation]`/`[MechanismUI]`/`[StationUI]`/`[ParamView]`/`[AlarmInfo]` → UI tự quét reflection dựng
nav/panel/field/metadata.
**Phương án khác.** Đăng ký thủ công từng màn/field trong code khởi tạo: dài dòng, dễ quên, dễ lệch.
**Vì sao chọn.** Thêm màn/cụm/tham số chỉ cần gắn attribute — không sửa nơi đăng ký trung tâm; lọc theo role tự động.
**Đánh đổi.** "Ma thuật" reflection khó lần theo hơn lời gọi tường minh; phụ thuộc quy ước tên assembly (`AM.Modules.*`).

## 6. Guard engine 3 tầng + RiskTier R0–R3 (an toàn theo dữ liệu, không if rải rác)
**Quyết định.** Mọi thao tác có hậu quả mang một `RiskTier`; `IGuardEngine.Evaluate(risk, condition?)` xét **trạng thái máy →
role → điều kiện phần cứng**. Quyền suy từ risk (R0=Operator…R3=Engineer); Force IO đòi Admin tại call site.
**Phương án khác.** Rải `if (level >= X && !running …)` khắp ViewModel: trùng lặp, dễ sót, khó audit.
**Vì sao chọn.** Một chỗ quyết định "được/không + lý do"; UI hiện *mờ + lý do* (giải thích thay vì giấu); mọi thao tác audit
đồng nhất; mở rộng tầng 3 không phá call site (tham số optional).
**Đánh đổi.** Phải khai risk cho mỗi thao tác; điều kiện tầng-3 hiện là mô hình bool (chưa DSL số).

## 7. HardwareInputEventBus — event-push, không polling (§S62)
**Quyết định.** Tín hiệu phần cứng (an toàn, cảm biến…) đẩy lên `IHardwareSignalBus` theo sự kiện; guard tầng 3 + UI đọc/observe.
**Phương án khác.** Poll trạng thái trong vòng lặp: tốn CPU, trễ, khó suy luận thời điểm.
**Vì sao chọn.** Đúng nguyên lý §9.3 tài liệu an toàn; chỉ phát khi giá trị đổi; tách nguồn (publisher) khỏi người đọc (guard/UI).
**Đánh đổi.** Cần adapter cho mỗi nguồn (vd `SafetySignalPublisher`); tín hiệu số phải quy về bool dẫn xuất.

## 8. i18n: `Loc` proxy + JSON catalog runtime (không hardcode chuỗi UI)
**Quyết định.** Chuỗi UI lấy từ `strings.{vi,en,zh}.json` qua proxy `Loc.Strings` (binding sống, đổi ngôn ngữ runtime);
alarm qua `Alarms.*.json`.
**Phương án khác.** .resx biên dịch (đổi ngôn ngữ phải khởi động lại) / chuỗi cứng trong XAML.
**Vì sao chọn.** Đổi ngôn ngữ tức thời; tách text khỏi code; người vận hành 3 thứ tiếng.
**Đánh đổi.** Mọi nhãn phải có key; quên key → hiện key. *Ngoại lệ có chủ đích:* định danh kỹ thuật (địa chỉ IO, `localize:false`)
giữ nguyên gốc — xem mục 12.

## 9. Force IO = chế độ riêng, tách khỏi set/reset (§S58–S61)
**Quyết định.** "Set/reset" (logic vẫn kiểm soát) và "Force" (đóng băng, cắt logic) là HAI việc khác bản chất: set/reset bấm
trực tiếp (Engineer); Force là *chế độ* bật riêng (Admin) + chạm-2-bước + alarm nhắc gỡ.
**Phương án khác.** Gộp một nút "ghi DO" cho cả hai: gọn nhưng người dùng không biết đang "bật tạm" hay "đóng băng vĩnh viễn" →
nguồn tai nạn kinh điển (quên gỡ force).
**Vì sao chọn.** Ranh giới hiển nhiên theo chế độ; force "dính" + nhắc gỡ; HAL chặn logic ghi đè kênh forced.
**Đánh đổi.** Nhiều trạng thái UI hơn; phải mở rộng `IIoModule` (ForceDo/Unforce).

## 10. Config-driven: IOMap / AxisMap / PointTable / MachineConfig / RecoveryActions
**Quyết định.** Dữ liệu đổi-theo-máy nằm trong JSON (đấu dây, trục, điểm dạy, layout, thao tác phục hồi), KHÔNG trong code.
**Phương án khác.** Hardcode trong WorkStation: đổi máy phải sửa+biên dịch; dễ lệch giữa thực tế và code.
**Vì sao chọn.** "Làm máy mới = đổi config"; kỹ thuật viên sửa đấu dây không cần lập trình viên; tái dùng UI/lõi 100%.
**Đánh đổi.** Lỗi cấu hình lộ lúc chạy (mitigate: validate khi nạp + fail-safe). Với thao tác (§6.3) chọn *hybrid* — xem [0002](0002-station-recovery-actions.md).

## 11. Build cứng: `TreatWarningsAsErrors` + `AnalysisMode=All`
**Quyết định.** Mọi cảnh báo CA/Sonar = lỗi build.
**Phương án khác.** Cảnh báo chỉ là cảnh báo: tích tụ nợ, bug an toàn lọt lưới.
**Vì sao chọn.** Phần mềm điều khiển máy — kỷ luật cao đáng giá; ép xử lý async/exception/null đúng ngay từ đầu.
**Đánh đổi.** Tốn thời gian dập analyzer (CA1812 DTO, S3267 LINQ, CA1716 keyword…); cần biết mẹo suppress đúng chỗ.

## 12. Quy ước tên: địa-chỉ-trước-tên, `localize:false` cho định danh kỹ thuật
**Quyết định.** HMI hiện `X017 · Chân không đầu hút` (địa chỉ mono, không dịch) trước tên có nghĩa; tên gốc nhà SX giữ qua
`rawName`/`localize:false`.
**Phương án khác.** Chỉ tên đã dịch / chỉ địa chỉ trần (`X000`): một bên khó dò dây, một bên thiếu ngữ cảnh.
**Vì sao chọn.** Địa chỉ là "mỏ neo" khớp nhãn tủ điện khi dò dây — xuyên qua mọi bản dịch; tên có nghĩa cho người vận hành.
**Đánh đổi.** Cần IOMap mang nhiều metadata (đã làm ở §S60).

---

## Phụ lục — bản đồ session → quyết định
| Session | Quyết định liên quan |
|---------|----------------------|
| S56–S57 | Guard engine R0–R3 (mục 6) + gate Motion/QuickActions |
| S58–S61 | Force IO chế độ riêng (mục 9) + IOMap mở rộng (mục 10, 12) |
| S62 | HardwareInputEventBus + guard tầng 3 (mục 6, 7) |
| S63 | Thao tác trạm RecoveryActions — [0002](0002-station-recovery-actions.md) |
