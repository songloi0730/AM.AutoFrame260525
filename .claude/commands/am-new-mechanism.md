# /am-new-mechanism

Tạo Mechanism mới (cụm cơ học) trong AM.WorkStation.

## Usage
```
/am-new-mechanism {MachineName} {MechanismName} {StationGroup} {Description}
```

## What this command does
1. Read skill `am-mechanism-patterns` for template
2. Create `{MechanismName}Mechanism.cs` in `AM.WorkStation.{MachineName}/Mechanisms/`
3. Add `[MechanismUI("{DisplayName}", group: "{StationGroup}", order: N)]` attribute
4. Inject hardware via `IHardwareManagerService.Resolve<T>("name")`
5. Expose domain methods (PickAsync, PlaceAsync, InspectAsync...) — NOT raw hardware
6. Implement `IMechanism`: `InitializeAsync`, `HomeAsync`, `EmergencyStop`
7. Create unit test skeleton

## Examples
```
/am-new-mechanism Demo Pick "Station A" "Pick part from feeder tray"
/am-new-mechanism Demo Inspect "Station A" "Vision inspection with camera"
/am-new-mechanism Demo Place "Station B" "Place part into output jig"
```

## Rules
- `EmergencyStop()` must NEVER throw — wrap all hardware calls in try-catch
- Expose DOMAIN methods, not raw hardware (`PickAsync` not `_motion.MoveAbsAsync`)
- `IHardwareManagerService.Resolve<T>()` for hardware — not direct DI injection
