# /am-alarm

Thêm alarm code mới vào hệ thống.

## Usage
```
/am-alarm {CODE_NAME} {LEVEL} {Station} "{vi-VN message}" "{en-US message}"
```
Level: Critical | High | Medium | Low

## What this command does
1. Add `[AlarmInfo("{en-US}", "{remedy}", AlarmAction.{Continue|Pause|Stop|ResetRequired})]` attribute
2. Add `public const int {CODE_NAME} = {N}` to `AM.Core/Constants/AlarmCodes.cs`
3. Show where to throw: `throw new AlarmException(AlarmCodes.{CODE_NAME}, station, msg)`

## Alarm code ranges
```
10000–10999  Motion/Axis       20000–20999  Vision/Camera
30000–30999  IO/Sensor         40000–40999  System/Application
50000–50999  Communication     60000–60999  Production/Recipe
70000–70999  Safety/Interlock
```

## Examples
```
/am-alarm PickSensorTimeout High AXIS_X "Cảm biến pick timeout sau {0}ms" "Pick sensor timeout after {0}ms"
/am-alarm VisionScoreLow Medium CAMERA_1 "Điểm vision thấp: {0:F1}" "Vision score low: {0:F1}"
/am-alarm DoorOpen Critical SAFETY "Cửa bảo vệ đang mở" "Safety door is open"
```
