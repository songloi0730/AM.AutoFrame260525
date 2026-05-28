# /am-new-station

Tạo Station mới (công đoạn) trong AM.WorkStation.

## Usage
```
/am-new-station {MachineName} {StationName} {Order} {Description}
```

## What this command does
1. Read skill `am-station-patterns` for template
2. Create `{StationName}Station.cs` in `AM.WorkStation.{MachineName}/Stations/`
3. Add `[StationUI("{DisplayName}", icon: "{icon}", order: {Order})]` attribute
4. Inherit `StationBase<{StationName}Station>`
5. Inject Mechanisms (not hardware directly)
6. Implement `ProcessNormalLoopAsync` and `ProcessDryRunLoopAsync`
7. Add pipeline sync calls via `IStationSyncService`
8. Create unit test skeleton

## Examples
```
/am-new-station Demo Feed 1 "Load parts from cassette to conveyor"
/am-new-station Demo PickPlace 2 "Pick from feeder, inspect, place to jig"
/am-new-station Demo Outfeed 3 "Sort OK/NG parts to output bins"
```

## Rules
- Station ONLY calls Mechanism methods — never hardware interfaces directly
- Use `IStationSyncService.Signal/WaitAsync` for pipeline sync with other stations
- Assign `CurrentStepDescription` before each sub-step for HMI display
- Both ProcessNormalLoopAsync AND ProcessDryRunLoopAsync must be implemented
