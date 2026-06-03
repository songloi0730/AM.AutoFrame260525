# libs/ — Thư viện vendor (DLL / SDK bản quyền)

> ⚠️ Thư mục này **KHÔNG được commit lên Git** (đã thêm vào `.gitignore`).
> Mỗi developer cần tự copy DLL từ SDK của vendor vào đúng vị trí.
> Chỉ commit file `*.placeholder` và `README.md`.

---

## Cấu trúc

```
libs/
├── Motion/
│   ├── Gts/              — 固高 Googoltech GTS-400/800 (gts.dll)
│   │   ├── x64/          — 64-bit DLL  ← dùng khi build x64 (mặc định)
│   │   └── x86/          — 32-bit DLL  ← dùng khi build x86 (nếu cần)
│   └── Advantech/        — Advantech PCI-1245/1265 (ADVMOT.dll)
│       ├── x64/
│       └── x86/
├── Vision/
│   ├── Cognex/           — Cognex VisionPro (Cognex.VisionPro.dll + CogSocketServer.dll)
│   ├── HIK/              — HIK Robot / MVS (MvCameraControl.Net.dll + MVSDKmd.dll)
│   └── Basler/           — Basler Pylon (PylonC.NET.dll)
└── IO/
    ├── Advantech-ADAM/   — ADAM-6000 series (qua Modbus TCP — không cần DLL)
    ├── Mitsubishi-QSeries/ — Mitsubishi Q series (MX Component hoặc qua Modbus)
    └── Omron-NX/         — Omron NX EtherCAT (FINS hoặc OPC UA)
```

---

## Hướng dẫn cài đặt từng vendor

### 固高 GTS (gts.dll)

1. Cài **GTS Motion Control Card SDK** từ CD đi kèm hoặc website 固高科技
2. Tìm `gts.dll` trong thư mục cài đặt (thường `C:\GTS\Lib\`)
3. Copy vào `libs/Motion/Gts/x64/gts.dll` (64-bit)
4. Copy `gts.dll` 32-bit vào `libs/Motion/Gts/x86/gts.dll` (nếu cần)

**Phiên bản đang dùng:** GTS-800-PG-E (hoặc GTS-400-PG)
**DLL:** `gts.dll` (không có NuGet, P/Invoke trực tiếp)
**Code reference:** `AM.Hardware.Motion/Gts/GtsNative.cs`

---

### Advantech Motion (ADVMOT.dll)

1. Cài **Advantech Common Motion API** từ [support.advantech.com](https://support.advantech.com)
2. Tìm `ADVMOT.dll` trong `C:\ADVMOT\` hoặc thư mục cài đặt
3. Copy vào `libs/Motion/Advantech/x64/ADVMOT.dll`

**Card hỗ trợ:** PCI-1245, PCI-1265, PCIe-1245, PCIe-1265
**DLL:** `ADVMOT.dll` (P/Invoke)
**Code reference:** `AM.Hardware.Motion/Advantech/AdvantechNative.cs`

---

### Cognex VisionPro

1. Cài **Cognex VisionPro** (yêu cầu license)
2. DLL thường ở `C:\Program Files\Cognex\VisionPro\bin\`
3. Copy vào `libs/Vision/Cognex/x64/`:
   - `Cognex.VisionPro.dll`
   - `Cognex.VisionPro.Core.dll`
   - `CogSocketServer.dll`

**Phiên bản:** VisionPro 9.x hoặc 10.x
**NuGet:** Không có — chỉ local DLL reference
**Code reference:** `AM.Hardware.Vision/` (TODO: CognexCamera.cs)

---

### HIK Robot / MVS Camera

1. Tải **HIK Robot MVS SDK** từ [hikmvs.cn](https://www.hikmvs.cn)
2. Copy từ `C:\Program Files\MVS\Development\DotNet\`:
   - `MvCameraControl.Net.dll` → `libs/Vision/HIK/x64/`
   - `MVSDKmd.dll` → `libs/Vision/HIK/x64/` (native dependency)

**Code reference:** `AM.Hardware.Vision/` (TODO: HIKCamera.cs)

---

### Basler Pylon

1. Cài **Basler Pylon Camera Software Suite**
2. DLL ở `C:\Program Files\Basler\pylon 7\Development\DotNet\`:
   - `PylonC.NET.dll` → `libs/Vision/Basler/x64/`

**NuGet alternative:** `Basler.Pylon` NuGet package (khuyến nghị dùng NuGet thay DLL local)
**Code reference:** `AM.Hardware.Vision/` (TODO: BaslerCamera.cs)

---

### Advantech ADAM I/O

ADAM-6000 series sử dụng **Modbus TCP** — không cần DLL đặc biệt.
Driver đã có sẵn trong `AM.Hardware.IO/Advantech/AdvantechAdamIoModule.cs`
(dùng `IModbusClient` qua `ModbusTcpClient`).

---

### Mitsubishi Q Series

Dùng **Mitsubishi MC Protocol 3E** qua TCP — không cần DLL.
Driver: `AM.Hardware.Comm/Mitsubishi/MitsubishiPlcDevice.cs`

---

### Siemens S7

Dùng **S7.Net Plus** NuGet package hoặc ISO-on-TCP trực tiếp.
Driver: `AM.Hardware.Comm/Siemens/SiemensS7PlcDevice.cs`

---

## Cấu hình .csproj sau khi copy DLL

```xml
<!-- AM.Hardware.Motion/AM.Hardware.Motion.csproj -->
<ItemGroup Condition="'$(Platform)' == 'x64' Or '$(Platform)' == 'AnyCPU'">
  <!-- GTS motion card -->
  <Content Include="..\..\libs\Motion\Gts\x64\gts.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>gts.dll</Link>
  </Content>
  <!-- Advantech motion card -->
  <Content Include="..\..\libs\Motion\Advantech\x64\ADVMOT.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>ADVMOT.dll</Link>
  </Content>
</ItemGroup>
```

> Uncomment phần tương ứng trong `.csproj` sau khi đã copy DLL vào thư mục.

---

## Kiểm tra DLL đã sẵn sàng

```powershell
# Chạy trong PowerShell từ thư mục gốc
Get-ChildItem -Path libs -Recurse -Filter "*.dll" | 
  Select-Object FullName, Length, LastWriteTime
```

Nếu kết quả trống → chưa copy DLL, driver thật sẽ fail khi chạy (nhưng Simulated driver vẫn hoạt động).
