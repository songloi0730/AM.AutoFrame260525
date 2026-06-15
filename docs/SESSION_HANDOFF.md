# SESSION HANDOFF — mạch HMI v2 + Guard Engine (S44–S57)

> **Mục đích:** tài liệu bàn giao để session MỚI tiếp tục mà không mất ngữ cảnh. Đọc file này +
> `PROJECT_STATUS.md` + `CHANGELOG.md` (mục Session 44→57) là đủ để bắt tay.
> Cập nhật: 2026-06-14, sau Session 57. Commit gần nhất: `19edfa9`.

---

## 0. Đọc theo thứ tự khi vào session mới
1. `PROJECT_STATUS.md` — snapshot.
2. File này — mạch HMI/guard + bẫy + roadmap.
3. `docs/HMI_Master_Index.md` **§11** — quyết định adoption (build gì / map gì / hoãn gì). NGUỒN DUY NHẤT.
4. Khi chạm an toàn/vận hành tay: `docs/HMI_Manual_Operation_and_Safety_v1.0.md`.
5. `CLAUDE.md` — build rules, conventions.

---

## 1. Trạng thái hiện tại (đã xong)

**Khung Shell v2 (7 vùng Persistent Frame)** — `AM.Application.Shell/MainWindow.xaml(.cs)` + `ShellViewModel.cs`:
header (logo · AUTO/DRY · LOCAL tĩnh · state badge ISA-88 · recipe · clock+heartbeat · ngôn ngữ · User) →
nav tab ngang → alarm banner multi-alarm (1 alarm ưu tiên cao nhất chưa ACK + "+N") → content →
action bar (Start/Pause-Resume/Stop/Reset · Dry run · Manual) → connection bar (Thiết bị│Host + version).

**Nav 8 tab** (auto-discovery từ `[ModuleNavigation]`, lọc theo role):
| key | nhãn | order | module | role |
|-----|------|-------|--------|------|
| Nav.Dashboard | Bảng điều khiển (Home) | 10 | Dashboard | all |
| Nav.Production | Sản xuất | 15 | Production | all |
| Nav.Vision | Vision | 18 | Vision | all |
| Nav.Alarms | Cảnh báo | 20 | Alarm | all |
| Nav.ManualOp | **Vận hành tay** | 40 | Motion | **minLevel=LineLead** |
| Nav.Parameter | Recipe | 50 | Parameter | all |
| Nav.Logging | Nhật ký | 75 | Logging | all |
| Nav.Settings | Cài đặt | 95 | Settings | all (host Chẩn đoán+Kỹ thuật) |

**Login** = overlay dialog (bấm nút User ở header → `LoginOverlay` trong `MainWindow.xaml`, phủ Grid.Row=3,
không che alarm/nav). Module Identity KHÔNG còn nav tab. Đăng xuất: bấm User → panel đã-đăng-nhập → Đăng xuất.

**Vận hành tay** (`AM.Modules.Motion`): dải khóa trạng thái + sub-tab: Điều khiển trục · Bảng điểm ·
**Giám sát I/O** (nhúng `IoMonitorView`) · Thao tác trạm (empty-state) · ⚠ Override (empty-state).
Điều khiển trục: bảng đèn 8 tín hiệu + servo/home/clear/move + jog/inching + phản hồi (qua `IAxisDiagnostics`).
Bảng điểm Set/Confirm 2-chạm.

**Guard engine R0–R3** (S56–S57) — XEM §3.

**Hạ tầng**: palette v2 (`App.xaml`, GIỮ TÊN token cũ), i18n vi/en/zh (`lang/strings.*.json`),
30 projects, **189 tests pass**, build 0 error/0 warning.

**Seed user** (auto-migrate, xem §4): operator/operator123 (Operator) · linelead/linelead123 (LineLead) ·
engineer/engineer123 (Engineer) · admin/admin123 (Administrator). SuperUser có trong enum, KHÔNG seed.

---

## 2. UserLevel (4 role + SuperUser OEM)
`Null=-1 · Operator=0 · LineLead=1 · Engineer=2 · Administrator=3 · SuperUser=4` (`AM.Core/Enums/UserLevel.cs`).
RBAC luôn dùng TÊN enum (`>= UserLevel.X`), KHÔNG hardcode int.

---

## 3. Guard engine — đã build, cách mở rộng

**Mô hình** (HMI_Manual_Operation_and_Safety §1.3): 3 tầng **trạng thái máy → role → guard condition**.
Hiện build tầng 1+2; tầng 3 (điều kiện phần cứng) là HOOK chưa nối.

**Files:**
- `AM.Core/Enums/RiskTier.cs` — `RiskTier{R0,R1,R2,R3}` + `GuardBlock{None,MachineBusy,InsufficientRole,ConditionNotMet}`.
- `AM.Core/Models/GuardResult.cs` — `(Allowed, Block, RequiredLevel)`.
- `AM.Core.Abstractions/.../IGuardEngine.cs` — `Evaluate(risk)`, `MinLevelFor(risk)`.
- `AM.Core.Abstractions/.../IAuditService.cs` — `Record(user, action, allowed, detail)`.
- `AM.Services/GuardService.cs` — map **R0=Operator·R1=LineLead·R2=Engineer·R3=Engineer**; R0 chạy được cả khi
  máy chạy, R1+ cần máy KHÔNG ở {Running,Initializing,Resetting}. (Force IO=Admin xử riêng tại call site.)
- `AM.Services/AuditService.cs` — log `[AUDIT] user=.. action=.. result=OK/DENIED detail=..` qua Serilog.
- DI: đăng ký ở `ServiceCollectionExtensions.AddCoreServices` (singleton).
- Test: `AM.Services.Tests/GuardServiceTests.cs` (13 — ma trận role×tier×state).

**Đã gắn:**
- `MotionViewModel`: `RefreshLockState()` dùng `_guard.Evaluate(R2)`; mọi lệnh trục qua
  `RunGuardedAsync(risk, action, body)` (jog/servo/teach=R3, move/home/clear/goto=R2; **STOP không gate**).
  Bị chặn → status lý do + audit DENIED, KHÔNG gọi HAL. Cho phép → audit OK + chạy.
- `DashboardViewModel`: QuickActions gán `RiskTier` (đèn/còi/ion/gọiKT=R0, cửa=R1); `RefreshQuickActions()` tính
  IsEnabled + SubText (chưa-HAL → cần-quyền/máy-chạy → chú thích); `QuickAction` command guard+audit.

**Cách thêm guard cho một thao tác mới:** gọi `_guard.Evaluate(RiskTier.Rx)` trước khi gọi HAL; nếu
`!Allowed` thì hiện lý do (map `GuardBlock` → i18n) + `_audit.Record(...DENIED)` + return; nếu Allowed thì
`_audit.Record(...OK)` rồi chạy. Inject `IGuardEngine`+`IAuditService` (+`IUserService` để lấy tên user).

---

## 4. BẪY đã gặp — ĐỌC KỸ trước khi sửa

1. **Cross-thread `UserChanged`** ⚠ quan trọng: `UserService.LoginAsync` dùng `await Task.Run(BCrypt.Verify)
   .ConfigureAwait(false)` → `UserChanged?.Invoke` bắn trên **thread nền**. MỌI subscriber đụng UI PHẢI marshal
   (Dispatcher.Invoke hoặc SynchronizationContext.Post). MainWindow đã `Dispatcher.Invoke`; ShellVM/IdentityVM/
   MotionVM/DashboardVM dùng `RunOnUi*`. Nếu thêm handler UserChanged đụng control mà QUÊN marshal → cross-thread
   exception → multicast delegate DỪNG → các handler sau không chạy (từng gây "Lỗi đăng nhập" giả + nav không rebuild).
2. **users.json**: envelope `{schemaVersion:2, users:[...]}`, `Level` lưu CHUỖI tên enum (`JsonStringEnumConverter`).
   Nếu reorder `UserLevel` lần nữa → **tăng `UserService.CurrentSchemaVersion`** để file cũ tự re-seed. File mảng cũ
   `[...]` tự bị phát hiện → re-seed.
3. **Build cứng** (`TreatWarningsAsErrors`+`AnalysisMode=All`): lỗi analyzer hay gặp phiên này —
   `S1135` (CHỮ "TODO" trong comment = LỖI, dùng "hoãn/sẽ bổ sung") · `S3358` (cấm nested ternary → if/else) ·
   `S2589` (điều kiện thừa) · `S2325` (handler XAML bị bảo "nên static" → `[SuppressMessage("Minor Code Smell","S2325")]`) ·
   `CA2263` (dùng `new T()` thay `Activator.CreateInstance(typeof(T))`) · `S1244` (float `==` → `Math.Abs(..)<eps`) ·
   `CA1716` (param tên trùng keyword vd `on` → `enabled`) · `MC3024` XAML (KHÔNG set vừa `Style="..."` attribute
   vừa inline `<X.Style>` — bỏ attribute).
4. **Chạy app để verify**: launch với **WorkingDirectory = bin\Debug** (nếu không sẽ dùng `users.json`/`points.json`
   ở repo-root). `dotnet test` KHÔNG rebuild Shell.exe → chạy `dotnet build` trước. **Dừng app trước khi build**
   (nó lock DLL): `Get-Process -Name AM.Application.Shell | Stop-Process -Force`.
5. **Computer-use bị mask**: trong session của AI, cửa sổ app bị che đen khi screenshot → KHÔNG verify trực quan
   được. Verify bằng build+test+đọc log (`bin/Debug/logs/automachine-*.log`, tìm `[AUDIT]`, `ERR`, exception).
   User tự chụp màn hình.

---

## 5. Workflow (BẮT BUỘC cuối mỗi session)
1. Cập nhật `PROJECT_STATUS.md` (header Session + Commit) + thêm entry `CHANGELOG.md`.
2. `bash scripts/am-commit.sh "loại: mô tả - Session N"` (tự add+commit+push; xử lý index.lock Windows).
3. Điền hash thật vào PROJECT_STATUS/CHANGELOG → commit thứ 2 `"docs: fill hash <hash> session N"`.
> Slash `/am-done` tự động 3 bước. Commit message kết bằng `Co-Authored-By: Claude Opus 4.8`.

**Thêm 1 tab/module mới** (mẫu Vision/Settings): tạo project `AM.Modules.X` (csproj net9.0-windows UseWPF) +
`XView.xaml(.cs)` `[ModuleNavigation("Nav.X", icon, order[, minLevel])]` + `XViewModel.cs` → `dotnet sln add` →
Shell csproj `ProjectReference` → đăng ký VM ở `ServiceCollectionExtensions.AddUiViewModels` → icon hex ở
`MainWindow.IconHex` → i18n `Nav.X` (vi/en/zh). `NavigationBuilder` chỉ quét assembly tên `AM.Modules.*`.
**Nhúng module vào sub-tab khác:** inject VM con vào VM cha, expose property, host View con `DataContext="{Binding ...}"`.

---

## 6. ROADMAP — phần còn HOÃN (ưu tiên giảm dần)

> Tất cả ghi ở `HMI_Master_Index.md §11C`. 2 câu hỏi §9 CẦN CHỦ DỰ ÁN CHỐT trước khi làm Override:
> (a) Override confirm = 1 người (2-bước+đếm-ngược) hay 2 người (giữ-nút)? (b) R2 — đã chốt CỨNG Engineer (S56).

1. ~~**Force IO = Admin** trong sub-tab Giám sát I/O~~ ✅ **XONG (S58, phương án A)**: `IoMonitorViewModel`
   inject `IGuardEngine`+`IAuditService`+`IUserService`; `ToggleOutput` gate `Evaluate(R3)` + check
   `>= Administrator` tại call site + audit OK/DENIED; dải khóa `LockText` + disable nút DO. **CÒN HOÃN:**
   "Force mode" THẬT (force/đóng băng từng kênh, khác write-DO thường) cần mở rộng `IIoModule` (hiện chỉ WriteDO)
   — xem `hmi_io_states.html` + Master Index §5. Khi mở rộng HAL: thêm khái niệm Forced/Frozen + badge trên kênh.
2. **HardwareInputEventBus + guard condition (tầng 3)**: nền cho thao tác trạm + override. Thêm cơ chế event-push
   các tín hiệu (vị trí Z, cảm biến chân không...) để guard đọc; mở rộng `IGuardEngine.Evaluate(risk, guardKey)`
   + `IGuardContext`. Hiện chỉ có `ISafetyInput.SafetyStateChanged` (E-Stop/Guard/LightCurtain).
3. **Thao tác trạm R1** (sub-tab "Thao tác trạm" đang empty-state): cần `RecoveryActions` config (id/risk/halCommand/
   guard/blockReason/audit) + HAL (vacuum/cylinder/conveyor). Dùng chung kiểu `GuardedAction` với QuickActions.
4. **Supervised Override** (sub-tab "⚠ Override" empty-state): luồng 2-bước + đếm ngược (mặc định 1 người),
   Engineer+, chỉ STOPPED, audit nặng + lý do. CHỜ chốt §9(a).
5. **QuickActions HAL** (5/6 nút "chưa cấu hình HAL") + **hold-to-confirm 1s** cho cửa (R1): cần wire IO thật.
   Cập nhật `DashboardViewModel.HasHal()` khi có HAL.
6. **Cài đặt GridMenu mở rộng**: thẻ Phần cứng/Hiệu chuẩn/Người dùng/Host(GEM-MES-OPC)/Sao lưu đang placeholder
   (`SettingsView.xaml`).
7. **Vision live-view**: sim `GrabImageAsync` trả `Array.Empty<byte>()` → vùng ảnh placeholder. Cần vision service
   trả frame thật (`FrameData`→`BitmapSource`).
8. **Nhỏ/UX**: tên trục có nghĩa (AX_0→AX_X_Adjust qua `IAxisMap`) · seed `points.json` demo (bảng điểm rỗng) ·
   `UiScale` theo machine config · LOCAL/REMOTE + popup GEM · tiến độ lô header (MES) · heartbeat đổi amber khi
   mất cập nhật >3s · billboard mode.

---

## 7. Lệnh nhanh
```powershell
# build (dừng app trước)
Get-Process -Name AM.Application.Shell -EA SilentlyContinue | Stop-Process -Force
dotnet build AM.AutoFrame.sln -v:m -nologo
# test
dotnet test AM.AutoFrame.sln --no-build -v:q -nologo
# chạy app (working dir bin\Debug để dùng đúng users.json/points.json)
Start-Process -FilePath ".\bin\Debug\AM.Application.Shell.exe" -WorkingDirectory ".\bin\Debug"
```
