using System.Text.Json.Serialization;
using Newtonsoft.Json.Linq;


namespace Squidcup
{

    public class MatchConfig
    {
        [JsonPropertyName("maplist")]
        public List<string> Maplist { get; set; } = new List<string>();

        [JsonPropertyName("maps_pool")]
        public List<string> MapsPool { get; set; } = new List<string>();

        [JsonPropertyName("maps_left_in_veto_pool")]
        public List<string> MapsLeftInVetoPool { get; set; } = new List<string>();

        [JsonPropertyName("map_ban_order")]
        public List<string> MapBanOrder { get; set; } = new List<string>();

        [JsonPropertyName("skip_veto")]
        public bool SkipVeto { get; set; } = true;

        [JsonPropertyName("match_id")]
        public long MatchId { get; set; }

        // Always 1 for BO1 - removed from JSON parsing
        public int NumMaps { get; set; } = 1;

        [JsonPropertyName("players_per_team")]
        public int PlayersPerTeam { get; set; } = 5;

        [JsonPropertyName("min_players_to_ready")]
        public int MinPlayersToReady { get; set; } = 12;

        [JsonPropertyName("min_spectators_to_ready")]
        public int MinSpectatorsToReady { get; set; } = 0;

        // Always 0 for BO1 - no map progression
        public int CurrentMapNumber { get; set; } = 0;

        [JsonPropertyName("map_sides")]
        public List<string> MapSides { get; set; } = new List<string>();

        // Not needed for BO1 - removed from JSON parsing
        public bool SeriesCanClinch { get; set; } = true;

        [JsonPropertyName("scrim")]
        public bool Scrim { get; set; } = false;
        
        [JsonPropertyName("wait_for_map")]
        public bool WaitForMap { get; set; } = true;

        [JsonPropertyName("gamemode")]
        public string GameMode { get; set; } = "5v5";

        [JsonPropertyName("match_side_type")]
        public string MatchSideType { get; set; } = "standard";

        [JsonPropertyName("changed_cvars")]
        public Dictionary<string, string> ChangedCvars { get; set; } = new();

        [JsonPropertyName("original_cvars")]
        public Dictionary<string, string> OriginalCvars { get; set; } = new();

        [JsonPropertyName("spectators")]
        public JToken Spectators { get; set; } = new JObject();

        [JsonPropertyName("remote_log_url")]
        public string RemoteLogURL { get; set; } = "";

        [JsonPropertyName("remote_log_header_key")]
        public string RemoteLogHeaderKey { get; set; } = "";

        [JsonPropertyName("remote_log_header_value")]
        public string RemoteLogHeaderValue { get; set; } = "";

        [JsonPropertyName("match_end_route")]
        public string MatchEndRoute { get; set; } = "";
    }
}