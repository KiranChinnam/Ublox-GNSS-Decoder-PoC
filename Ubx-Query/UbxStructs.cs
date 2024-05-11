using System.Runtime.CompilerServices;

namespace Ubx_Query;

public struct UbxNavPvt
{
    public DateTimeOffset UtcTime { get; set; }
    public bool ValidDate { get; set; }
    public bool ValidTime { get; set; }
    public bool TimeFullyResolved { get; set; }
    public bool ValidMagneticDeclination { get; set; }
    public uint TimeAccuracy { get; set; }
    public UbxNavPvtFixType FixType { get; set; }
    public bool GnssFixOk { get; set; }
    public bool DifferentialCorrectionsApplied { get; set; }
    public byte SatellitesUsed { get; set; }
    public float Longitude { get; set; }
    public float Latitude { get; set; }
    public uint HorizontalAccuracyMm { get; set; }
    public uint VerticalAccuracyMm { get; set; }
}

public enum UbxNavPvtFixType
{
    NoFix = 0,
    DeadReckoning = 1,
    Fix2D = 2,
    Fix3D = 3,
    GnssDeadReckoning = 4,
    TimeOnly = 5
}