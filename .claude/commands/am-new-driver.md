# /am-new-driver

Tạo hardware driver hoàn chỉnh cho AM.AutoFrame.

## Usage
```
/am-new-driver {DeviceName} {Category} {SDKLibrary}
```

## Categories
`Motion` | `Vision` | `IO` | `Communication`

## What this command does
1. Read skill `am-hardware-patterns` for templates
2. Create `I{DeviceName}.cs` → `AM.Core.Abstractions/Interfaces/Hardware/`
3. Create `{DeviceName}.cs` → `AM.Hardware.{Category}/`
4. Create `Simulated{DeviceName}.cs` → `AM.Hardware.{Category}/` (same folder)
5. Add alarm codes to `AM.Core/Constants/AlarmCodes.cs` (correct range)
6. Add DI registration comment for `AM.Application.Shell/Bootstrapper.cs`
7. Create unit test skeleton in `tests/`

## Examples
```
/am-new-driver LtdmcMotion Motion LtdmcSDK
/am-new-driver HikCamera Vision HikRobotMVS
/am-new-driver ModbusTcpClient Communication NModbus4
/am-new-driver OmronNxIO IO OmronFinsLibrary
```

## Output guarantees
- `using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct)` (CA2000)
- `ArgumentNullException.ThrowIfNull()` in constructor
- `[SuppressMessage("Security","CA5394",...)]` on simulator Random
- `IAsyncDisposable` implemented
- File header comment on all 3 files
