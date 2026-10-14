namespace ArcSperanzaIntel.Core.Models;

/// <summary>Wydarzenie na mapie z harmonogramu MetaForge (Night Raid, Matriarch, ARC Frigate…).</summary>
public sealed record GameEvent(string Name, string MapName, string? MapId, string? IconUrl, DateTimeOffset Start, DateTimeOffset End)
{
    public bool IsActive(DateTimeOffset now) => Start <= now && now < End;
    public bool IsUpcoming(DateTimeOffset now) => Start > now;
}
