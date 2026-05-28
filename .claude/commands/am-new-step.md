# /am-new-step

Tạo Step mới trong AM.WorkStation cho máy hiện tại.

## Usage
```
/am-new-step {NN} {StepName} {Description}
```

## What this command does
1. Read skill `am-sequence-patterns` for template
2. Create `Step{NN}{StepName}.cs` in `AM.WorkStation.{MachineName}/Steps/`
   - Class name: PascalCase, NO underscore (CA1707)
3. Inject required interfaces (IMotionController, ICameraDevice, IIoModule, etc.)
4. Add `Validate()` with precondition checks
5. Add `ExecuteAsync()` with per-step timeout wrapper
6. Create unit test skeleton in `tests/`

## Examples
```
/am-new-step 03 Initialize "Connect hardware, home all axes"
/am-new-step 05 Inspect "Capture image and run vision inspection"
/am-new-step 07 BarcodeVerify "Read barcode and verify against work order"
```

## Important
- Class named `Step05Inspect` — never `Step05_Inspect` (CA1707 is a build error)
- Inject only interfaces from `AM.Core.Abstractions` — never concrete hardware classes
- Each step must be atomic (all-or-nothing) and idempotent (safe to retry)
