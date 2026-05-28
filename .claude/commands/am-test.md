# /am-test

Tạo unit tests đầy đủ cho class hoặc method.

## Usage
```
/am-test {ClassName}           # Tests cho toàn bộ class
/am-test {ClassName}.{Method}  # Tests cho method cụ thể
```

## What this command does
1. Read skill `am-testing` for templates
2. Read target class to understand public API and dependencies
3. Generate test class with:
   - Happy path (thành công bình thường)
   - Edge cases (null, empty, boundary values: 0, max, -1)
   - Error paths (exception, timeout, hardware fail)
   - Cancellation (CancellationToken cancelled mid-operation)
   - `[Theory]` + `[InlineData]` for parametric cases
   - Mock repository interaction verification
4. Framework: xUnit + Moq + FluentAssertions
5. Naming: `{Method}_{Condition}_{ExpectedResult}`

## Coverage targets
```
AlarmService, ParameterService: ≥ 90%
Other Services:                 ≥ 80%
WorkStation Steps:              ≥ 80%
ViewModels:                     ≥ 70%
Hardware Simulators:            ≥ 50%
```

## Examples
```
/am-test AlarmService
/am-test AlarmService.RaiseAsync
/am-test Step05Inspect.ExecuteAsync
/am-test PickMechanism.PickAsync
```
