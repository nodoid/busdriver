namespace BusDriver.Core;

public enum SessionEventKind
{
    DoorsOpened,
    DoorsRefused,
    StopServed,
    OnTime,
    Late,
    TripComplete,
    Crash,
    Horn,
    ShiftEnd,
    Layover,
    Recovered
}

public readonly record struct SessionEvent(
    SessionEventKind Kind,
    string Message,
    int Value = 0);
