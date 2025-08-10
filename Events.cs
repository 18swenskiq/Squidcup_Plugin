using System.Text.Json.Serialization;

namespace Squidcup;
public class SquidcupEvent
{
    public SquidcupEvent(string eventName)
    {
        EventName = eventName;
    }

    [JsonPropertyName("event")]
    public string EventName { get; }
}

public class SquidcupMatchEvent : SquidcupEvent
{
    [JsonPropertyName("matchid")]
    public required long MatchId { get; init; }

    protected SquidcupMatchEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupMatchTeamEvent : SquidcupMatchEvent
{
    [JsonPropertyName("team")]
    public required string Team { get; init; }

    protected SquidcupMatchTeamEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupMapEvent : SquidcupMatchEvent
{
    [JsonPropertyName("map_number")]
    public required int MapNumber { get; init; }

    protected SquidcupMapEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupMapTeamEvent : SquidcupMapEvent
{
    [JsonPropertyName("team_int")]
    public required int TeamNumber { get; init; }

    protected SquidcupMapTeamEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupRoundEvent : SquidcupMapEvent
{
    [JsonPropertyName("round_number")]
    public required int RoundNumber { get; init; }

    protected SquidcupRoundEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupTimedRoundEvent : SquidcupRoundEvent
{
    [JsonPropertyName("round_time")]
    public required int RoundTime { get; init; }

    protected SquidcupTimedRoundEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupPlayerRoundEvent : SquidcupRoundEvent
{

    [JsonPropertyName("player")]
    public required int Player { get; init; }

    protected SquidcupPlayerRoundEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupPlayerTimedRoundEvent : SquidcupTimedRoundEvent
{
    [JsonPropertyName("player")]
    public required int Player { get; init; }

    protected SquidcupPlayerTimedRoundEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupPlayerDisconnectedEvent : SquidcupMatchEvent
{
    [JsonPropertyName("player")]
    public required int Player { get; init; }

    public SquidcupPlayerDisconnectedEvent() : base("player_disconnect")
    {
    }
}

public class SquidcupSeriesStartedEvent : SquidcupMatchEvent
{
    [JsonPropertyName("team1")]
    public required SquidcupTeamWrapper Team1 { get; init; }

    [JsonPropertyName("team2")]
    public required SquidcupTeamWrapper Team2 { get; init; }

    [JsonPropertyName("num_maps")]
    public required int NumberOfMaps { get; init; }

    public SquidcupSeriesStartedEvent() : base("series_start")
    {
    }
}

public class SquidcupSeriesResultEvent : SquidcupMatchEvent
{
    [JsonPropertyName("time_until_restore")]
    public required int TimeUntilRestore { get; init; }

    [JsonPropertyName("winner")]
    public required Winner Winner { get; init; }

    [JsonPropertyName("team1_series_score")]
    public required int Team1SeriesScore { get; init; }

    [JsonPropertyName("team2_series_score")]
    public required int Team2SeriesScore { get; init; }

    public SquidcupSeriesResultEvent() : base("series_end")
    {
    }
}

public class GoingLiveEvent : SquidcupMapEvent
{
    public GoingLiveEvent() : base("going_live")
    {
    }
}

public class SquidcupRoundEndedEvent : SquidcupTimedRoundEvent
{

    [JsonPropertyName("reason")]
    public required int Reason { get; init; }

    [JsonPropertyName("winner")]
    public required Winner Winner { get; init; }

    [JsonPropertyName("team1")]
    public required SquidcupStatsTeam StatsTeam1 { get; init; }

    [JsonPropertyName("team2")]
    public required SquidcupStatsTeam StatsTeam2 { get; init; }

    public SquidcupRoundEndedEvent() : base("round_end")
    {
    }
}

public class MapResultEvent : SquidcupMapEvent
{
    [JsonPropertyName("winner")]
    public required Winner Winner { get; init; }

    [JsonPropertyName("team1")]
    public required SquidcupStatsTeam StatsTeam1 { get; init; }

    [JsonPropertyName("team2")]
    public required SquidcupStatsTeam StatsTeam2 { get; init; }

    public MapResultEvent() : base("map_result")
    {
    }
}

public class SquidcupMapSelectionEvent : SquidcupMatchTeamEvent
{
    [JsonPropertyName("map_name")]
    public required string MapName { get; init; }

    protected SquidcupMapSelectionEvent(string eventName) : base(eventName)
    {
    }
}

public class SquidcupMapPickedEvent : SquidcupMapSelectionEvent
{
    [JsonPropertyName("map_number")]
    public required int MapNumber { get; init; }

    public SquidcupMapPickedEvent() : base("map_picked")
    {
    }
}

public class SquidcupMapVetoedEvent : SquidcupMapSelectionEvent
{
    public SquidcupMapVetoedEvent() : base("map_vetoed")
    {
    }
}

public class SquidcupSidePickedEvent : SquidcupMapSelectionEvent
{
    [JsonPropertyName("map_number")]
    public required int MapNumber { get; init; }

    [JsonPropertyName("side")]
    public required string Side { get; init; }

    public SquidcupSidePickedEvent() : base("side_picked")
    {
    }
}

public class SquidcupDemoUploadedEvent : SquidcupMatchEvent
{
    [JsonPropertyName("map_number")]
    public required int MapNumber { get; init; }

    [JsonPropertyName("filename")]
    public required string FileName { get; init; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    public SquidcupDemoUploadedEvent() : base("demo_upload_ended")
    {
    }
}