// -------------------------------------------------------
// File:    IoMap.cs
// Project: AM.WorkStation.Demo
// Purpose: Hằng số tên IO/trục logic của máy DemoPickPlace (DemoMachine_IO_Map §7 — chống magic string)
// -------------------------------------------------------

namespace AM.WorkStation.Demo.Config;

/// <summary>
/// Tên logic IO/trục của máy DemoPickPlace. Code chỉ dùng hằng số này —
/// địa chỉ vật lý map trong config theo vendor (HAL). Xem <c>docs/DemoMachine_IO_Map.md</c>.
/// Internal: chỉ station trong máy này dùng (CA1034/CA1716 với nested type public).
/// </summary>
internal static class IoMap
{
    /// <summary>Digital inputs.</summary>
    public static class Di
    {
        public const string EStopOk           = "DI.EStop.Ok";
        public const string SafetyDoorClosed  = "DI.SafetyDoor.Closed";
        public const string DoorLockLocked    = "DI.DoorLock.Locked";
        public const string AirPressureOk     = "DI.AirPressure.Ok";
        public const string BtnStart          = "DI.Btn.Start";
        public const string BtnStop           = "DI.Btn.Stop";
        public const string BtnReset          = "DI.Btn.Reset";
        public const string FeederTrayPresent = "DI.Feeder.TrayPresent";
        public const string FeederPartAtPick  = "DI.Feeder.PartAtPick";
        public const string NozzleVacuumOn    = "DI.Nozzle.VacuumOn";
        public const string OutTrayPresent    = "DI.OutTray.Present";
        public const string OutTrayFull       = "DI.OutTray.Full";
        public const string NgTrayPresent     = "DI.NgTray.Present";
    }

    /// <summary>Digital outputs.</summary>
    public static class Do
    {
        public const string TowerRed      = "DO.Tower.Red";
        public const string TowerYellow   = "DO.Tower.Yellow";
        public const string TowerGreen    = "DO.Tower.Green";
        public const string Buzzer        = "DO.Buzzer";
        public const string VacuumOn      = "DO.Vacuum.On";
        public const string VacuumBlow    = "DO.Vacuum.Blow";
        public const string DoorLock      = "DO.DoorLock";
        public const string WorkLight     = "DO.WorkLight";
        public const string Ionizer       = "DO.Ionizer";
        public const string CameraTrigger = "DO.Camera.Trigger";
        public const string FeederAdvance = "DO.Feeder.Advance";
    }

    /// <summary>Analog inputs.</summary>
    public static class Ai
    {
        public const string VacuumPressure = "AI.Vacuum.Pressure";
        public const string MainPressure   = "AI.Main.Pressure";
    }

    /// <summary>Trục logic.</summary>
    public static class Axis
    {
        public const string X = "Axis.X";
        public const string Y = "Axis.Y";
        public const string Z = "Axis.Z";
    }
}
