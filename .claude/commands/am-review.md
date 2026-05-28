# /am-review

Review code hiện tại theo AM.AutoFrame coding standards.

## Usage
```
/am-review              # Review file hiện đang mở
/am-review {filepath}   # Review file cụ thể
```

## Checklist (kiểm tra TẤT CẢ)
1. **Architecture**: layer dependencies, interface usage, 3-tier Mechanism/Station/MasterController
2. **Async**: CancellationToken, no .Result/.Wait(), `using var` for linked CTS (CA2000)
3. **Hardware**: timeout on every SDK call
4. **Exceptions**: 3-catch hierarchy, CA1031 pragma with justification, RSPEC-2139 filter
5. **Logging**: structured, correct level, exception as first arg (RSPEC-6667), no sensitive data
6. **Sonar rules**: CA1707, RSPEC-6602/6605, CA1869, S2365
7. **Security**: no hardcoded credentials, UserLevel checks before dangerous ops
8. **XAML** (if applicable): no hardcoded strings/colors
9. **Attributes**: [AlarmInfo], [MechanismUI], [StationUI], [ParamView] where needed
10. **Tests**: new public API has corresponding test

## Output format
```
Line {N}: [CRITICAL|WARNING|INFO] {issue} → {fix}
...
Summary: {C} critical, {W} warnings, {I} info
```

Type `fix` after review to apply WARNING/INFO fixes automatically.
CRITICAL issues require manual review.
