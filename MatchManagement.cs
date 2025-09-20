using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json.Linq;


namespace Squidcup
{

    public partial class Squidcup
    {
        public MatchConfig matchConfig = new();

        public bool isMatchSetup = false;

        public bool matchModeOnly = false;

        public bool resetCvarsOnSeriesEnd = true;

        public string loadedConfigFile = "";

        public Team squidcupTeam1 = new() {
            teamName = "COUNTER-TERRORISTS"
        };
        public Team squidcupTeam2 = new() {
            teamName = "TERRORISTS"
        };

        public Dictionary<Team, string> teamSides = new();
        public Dictionary<string, Team> reverseTeamSides = new();

        [ConsoleCommand("css_team1", "Sets team name for team1")]
        public void OnTeam1Command(CCSPlayerController? player, CommandInfo command) {
            HandleTeamNameChangeCommand(player, command.ArgString, 1);
        }

        [ConsoleCommand("css_team2", "Sets team name for team2")]
        public void OnTeam2Command(CCSPlayerController? player, CommandInfo command) {
            HandleTeamNameChangeCommand(player, command.ArgString, 2);
        }

        [ConsoleCommand("squidcup_loadmatch", "Loads a match from the given JSON file path (relative to the csgo/ directory)")]
        public void LoadMatch(CCSPlayerController? player, CommandInfo command)
        {
            try
            {
                if (player != null) return;
                if (isMatchSetup)
                {
                    // command.ReplyToCommand($"[LoadMatch] A match is already setup with id: {liveMatchId}, cannot load a new match!");
                    ReplyToUserCommand(player, Localizer["squidcup.mm.matchisalreadysetup", liveMatchId]);
                    Log($"[LoadMatch] A match is already setup with id: {liveMatchId}, cannot load a new match!");
                    return;
                }
                string fileName = command.ArgString;
                string filePath = Path.Join(Server.GameDirectory + "/csgo", fileName);
                if (!File.Exists(filePath)) 
                {
                    // command.ReplyToCommand($"[LoadMatch] Provided file does not exist! Usage: squidcup_loadmatch <filename>");
                    ReplyToUserCommand(player, Localizer["squidcup.mm.filedoesntexist"]);
                    Log($"[LoadMatch] Provided file does not exist! Usage: squidcup_loadmatch <filename>");
                    return;
                }
                string jsonData = File.ReadAllText(filePath);
                bool success = LoadMatchFromJSON(jsonData);
                if (!success)
                {
                    // command.ReplyToCommand("Match load failed! Resetting current match");
                    ReplyToUserCommand(player, Localizer["squidcup.mm.matchloadfailed"]);
                    ResetMatch();
                }
                loadedConfigFile = fileName;
            }
            catch (Exception e)
            {
                Log($"[LoadMatch - FATAL] An error occured: {e.Message}");
                return;
            }
        }

        [ConsoleCommand("get5_loadmatch_url", "Loads a match from the given URL")]
        [ConsoleCommand("squidcup_loadmatch_url", "Loads a match from the given URL")]
        public void LoadMatchFromURL(CCSPlayerController? player, CommandInfo command)
        {
            if (player != null) return;
            if (isMatchSetup)
            {
                // command.ReplyToCommand($"[LoadMatchDataCommand] A match is already setup with id: {liveMatchId}, cannot load a new match!");
                ReplyToUserCommand(player, Localizer["squidcup.mm.get5matchisalreadysetup", liveMatchId]);
                Log($"[LoadMatchDataCommand] A match is already setup with id: {liveMatchId}, cannot load a new match!");
                return;
            }
            string url = command.ArgByIndex(1);

            string headerName = command.ArgCount > 3 ? command.ArgByIndex(2) : "";
            string headerValue = command.ArgCount > 3 ? command.ArgByIndex(3) : "";

            Log($"[LoadMatchDataCommand] Match setup request received with URL: {url} headerName: {headerName} and headerValue: {headerValue}");

            if (!IsValidUrl(url))
            {
                // command.ReplyToCommand($"[LoadMatchDataCommand] Invalid URL: {url}. Please provide a valid URL to load the match!");
                ReplyToUserCommand(player, Localizer["squidcup.mm.invalidurl", url]);
                Log($"[LoadMatchDataCommand] Invalid URL: {url}. Please provide a valid URL to load the match!");
                return;
            }
            try
            {
                HttpClient httpClient = new();
                if (headerName != "")
                {
                    httpClient.DefaultRequestHeaders.Add(headerName, headerValue);
                }
                HttpResponseMessage response = httpClient.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    string jsonData = response.Content.ReadAsStringAsync().Result;
                    Log($"[LoadMatchFromURL] Received following data: {jsonData}");

                    bool success = LoadMatchFromJSON(jsonData);
                    if (!success)
                    {
                        // command.ReplyToCommand("Match load failed! Resetting current match");
                        ReplyToUserCommand(player, Localizer["squidcup.mm.matchloadfailed"]);
                        ResetMatch();
                    }
                    loadedConfigFile = url;
                }
                else
                {
                    // command.ReplyToCommand($"[LoadMatchFromURL] HTTP request failed with status code: {response.StatusCode}");
                    ReplyToUserCommand(player, Localizer["squidcup.mm.httprequestfailed", response.StatusCode]);
                    Log($"[LoadMatchFromURL] HTTP request failed with status code: {response.StatusCode}");
                }
            }
            catch (Exception e)
            {
                Log($"[LoadMatchFromURL - FATAL] An error occured: {e.Message}");
                return;
            }
        }

        static string ValidateMatchJsonStructure(JObject jsonData)
        {
            string[] requiredFields = { "maplist", "team1", "team2" };

            // Check if any required field is missing
            foreach (string field in requiredFields)
            {
                if (jsonData[field] == null)
                {
                    return $"Missing mandatory field: {field}";
                }
            }

            foreach (var property in jsonData.Properties())
            {
                string field = property.Name;

                switch (field)
                {
                    case "matchid":
                    case "players_per_team":
                    case "min_players_to_ready":
                    case "min_spectators_to_ready":
                        int value;
                        if (!int.TryParse(jsonData[field]!.ToString(), out value))
                        {
                            return $"{field} should be an integer!";
                        }
                        break;
                    
                    case "cvars":
                        if (jsonData[field]!.Type != JTokenType.Object)
                        {
                            return $"{field} should be a JSON structure!";
                        }
                        break;

                    case "team1":
                    case "team2":
                    case "spectators":
                        if (jsonData[field]!.Type != JTokenType.Object)
                        {
                            return $"{field} should be a JSON structure!";
                        }
                        if ((field != "spectators") && (jsonData[field]!["players"] == null || jsonData[field]!["players"]!.Type != JTokenType.Object)) 
                        {
                            return $"{field} should have 'players' JSON!";
                        }
                        break;

                    case "veto_mode":
                        if (jsonData[field]!.Type != JTokenType.Array)
                        {
                            return $"{field} should be an Array!";
                        }
                        break;

                    case "maplist":
                        if (jsonData[field]!.Type != JTokenType.Array)
                        {
                            return $"{field} should be an Array!";
                        }
                        if (!jsonData[field]!.Any())
                        {
                            return $"{field} should contain atleast 1 map!";
                        }

                        break;
                    case "map_sides":
                        if (jsonData[field]!.Type != JTokenType.Array)
                        {
                            return $"{field} should be an Array!";
                        }
                        string[] allowedValues = { "team1_ct", "team1_t", "team2_ct", "team2_t", "knife" };
                        bool allElementsValid = jsonData[field]!.All(element => allowedValues.Contains(element.ToString()));

                        if (!allElementsValid) {
                            return $"{field} should be \"team1_ct\", \"team1_t\", or \"knife\"!";
                        }
                        break;

                    case "skip_veto":
                    case "gamemode":
                        string gamemodeValue = jsonData[field]!.ToString();
                        string[] validGameModes = { "1v1", "wingman", "3v3", "5v5" };
                        if (!validGameModes.Contains(gamemodeValue.ToLower()))
                        {
                            return $"{field} should be one of: 1v1, wingman, 3v3, 5v5";
                        }
                        break;
                }
            }

            return "";
        }

        public bool LoadMatchFromJSON(string jsonData)
        {
            
            JObject jsonDataObject = JObject.Parse(jsonData);

            string validationError = ValidateMatchJsonStructure(jsonDataObject);

            if (validationError != "")
            {
                Log($"[LoadMatchDataCommand] {validationError}");
                return false;
            }

            if(jsonDataObject["matchid"] != null)
            {
                liveMatchId = (long)jsonDataObject["matchid"]!;
            }
            JToken team1 = jsonDataObject["team1"]!;
            JToken team2 = jsonDataObject["team2"]!;
            JToken maplist = jsonDataObject["maplist"]!;

            if (team1["id"] != null) squidcupTeam1.id = team1["id"]!.ToString();
            if (team2["id"] != null) squidcupTeam2.id = team2["id"]!.ToString();

            squidcupTeam1.teamName = RemoveSpecialCharacters(team1["name"]!.ToString());
            squidcupTeam2.teamName = RemoveSpecialCharacters(team2["name"]!.ToString());
            squidcupTeam1.teamPlayers = team1["players"];
            squidcupTeam2.teamPlayers = team2["players"];

            matchConfig = new()
            {
                MatchId = liveMatchId,
                MapsPool = maplist.ToObject<List<string>>()!,
                MapsLeftInVetoPool = maplist.ToObject<List<string>>()!,
                NumMaps = 1, // Always 1 for BO1
                MinPlayersToReady = minimumReadyRequired
            };

            GetOptionalMatchValues(jsonDataObject);

            // For BO1, always skip veto if only 1 map is provided
            if (matchConfig.MapsPool.Count == 1)
            {
                matchConfig.SkipVeto = true;
                isPreVeto = false;
            }
            else if (matchConfig.MapsPool.Count < 1)
            {
                Log($"[LOADMATCH] At least 1 map must be provided in the map pool.");
                return false;
            }

            if (!matchConfig.SkipVeto)
            {
                if (matchConfig.MapBanOrder.Count != 0)
                {
                    if (!ValidateMapBanLogic()) return false;
                }
                else
                {
                    GenerateDefaultVetoSetup();
                }
            }

            GetCvarValues(jsonDataObject);

            Log($"[LOADMATCH] MinPlayersToReady: {matchConfig.MinPlayersToReady}");
            Log($"[LOADMATCH] MapsPool: {string.Join(", ", matchConfig.MapsPool)} MapsLeftInVetoPool: {string.Join(", ", matchConfig.MapsLeftInVetoPool)}");

            LoadClientNames();

            if (matchConfig.SkipVeto)
            {
                // Copy the first k maps from the maplist to the final match maps.
                // For BO1, only one map iteration
                for (int i = 0; i < 1; i++) 
                {
                    matchConfig.Maplist.Add(matchConfig.MapsPool[i]);

                    // Push a map side if one hasn't been set yet.
                    if (matchConfig.MapSides.Count < matchConfig.Maplist.Count) {
                        if (matchConfig.MatchSideType == "standard" || matchConfig.MatchSideType == "always_knife") {
                            matchConfig.MapSides.Add("knife");
                        } else if (matchConfig.MatchSideType == "random") {
                            matchConfig.MapSides.Add(new Random().Next(0, 2) == 0 ? "team1_ct" : "team1_t");
                        } else {
                            matchConfig.MapSides.Add("team1_ct");
                        }
                    }
                }
                string currentMapName = Server.MapName;
                string mapName = matchConfig.Maplist[0].ToString();

                if (IsMapReloadRequiredForGameMode(matchConfig.GameMode) || mapReloadRequired || currentMapName != mapName) 
                {
                    SetCorrectGameMode();
                    ChangeMap(mapName, 0);
                }
            }
            else
            {
                isPreVeto = true;
            } 

            readyAvailable = true;

            // This is done before starting warmup so that cvars like get5_remote_log_url are set properly to send the events
            ExecuteChangedConvars();

            StartWarmup();

            isMatchSetup = true;

            if(matchConfig.SkipVeto) SetMapSides();

            SetTeamNames();
            UpdatePlayersMap();
            UpdateHostname();

            var seriesStartedEvent = new SquidcupSeriesStartedEvent
            {
                MatchId = liveMatchId,
                NumberOfMaps = 1, // Always 1 for BO1
                Team1 = new(squidcupTeam1.id, squidcupTeam1.teamName),
                Team2 = new(squidcupTeam2.id, squidcupTeam2.teamName),
            };

            Task.Run(async () => {
                await SendEventAsync(seriesStartedEvent);
            });

            Log($"[LoadMatchFromJSON] Success with matchid: {liveMatchId}!");
            return true;
        }

        public void SetMapSides() {
            int mapNumber = matchConfig.CurrentMapNumber;
            if (matchConfig.MapSides[mapNumber] == "team1_ct" || matchConfig.MapSides[mapNumber] == "team2_t")
            {
                teamSides[squidcupTeam1] = "CT";
                teamSides[squidcupTeam2] = "TERRORIST";
                reverseTeamSides["CT"] = squidcupTeam1;
                reverseTeamSides["TERRORIST"] = squidcupTeam2;
                isKnifeRequired = false;
            }
            else if (matchConfig.MapSides[mapNumber] == "team2_ct" || matchConfig.MapSides[mapNumber] == "team1_t")
            {
                teamSides[squidcupTeam2] = "CT";
                teamSides[squidcupTeam1] = "TERRORIST";
                reverseTeamSides["CT"] = squidcupTeam2;
                reverseTeamSides["TERRORIST"] = squidcupTeam1;
                isKnifeRequired = false;
            }
            else if (matchConfig.MapSides[mapNumber] == "knife")
            {
                isKnifeRequired = true;
            }

            SetTeamNames();
        }

        public void SetTeamNames()
        {
            Server.ExecuteCommand($"mp_teamname_1 {reverseTeamSides["CT"].teamName}");
            Server.ExecuteCommand($"mp_teamname_2 {reverseTeamSides["TERRORIST"].teamName}");
        }

        public void GetCvarValues(JObject jsonDataObject)
        {
            try
            {
                if (jsonDataObject["cvars"] == null) return;

                foreach (JProperty cvarData in jsonDataObject["cvars"]!)
                {
                    string cvarName = cvarData.Name;
                    string cvarValue = cvarData.Value.ToString();

                    var cvar = ConVar.Find(cvarName);
                    matchConfig.ChangedCvars[cvarName] = cvarValue;
                    if (cvar != null)
                    {
                        matchConfig.OriginalCvars[cvarName] = GetConvarStringValue(cvar);
                    }
                }

            }
            catch (Exception e)
            {
                Log($"[GetCvarValues FATAL] An error occurred: {e.Message}");
            }
        }

        public void GetOptionalMatchValues(JObject jsonDataObject)
        {
            if(jsonDataObject["map_sides"] != null)
            {
                matchConfig.MapSides = jsonDataObject["map_sides"]!.ToObject<List<string>>()!;
            }
            if(jsonDataObject["players_per_team"] != null)
            {
                matchConfig.PlayersPerTeam = jsonDataObject["players_per_team"]!.Value<int>();
            }
            if(jsonDataObject["min_players_to_ready"] != null)
            {
                matchConfig.MinPlayersToReady = jsonDataObject["min_players_to_ready"]!.Value<int>();
            }
            if(jsonDataObject["min_spectators_to_ready"] != null)
            {
                matchConfig.MinSpectatorsToReady = jsonDataObject["min_spectators_to_ready"]!.Value<int>();
            }
            if (jsonDataObject["spectators"] != null && jsonDataObject["spectators"]!["players"] != null)
            {
                matchConfig.Spectators = jsonDataObject["spectators"]!["players"]!;
                if (matchConfig.Spectators is JArray spectatorsArray && spectatorsArray.Count == 0)
                {
                    // Convert the empty JArray to an empty JObject
                    matchConfig.Spectators = new JObject();
                }
            }
            if (jsonDataObject["clinch_series"] != null)
            {
            // SeriesCanClinch removed - not needed for BO1
            }
            if (jsonDataObject["skip_veto"] != null)
            {
                matchConfig.SkipVeto = bool.Parse(jsonDataObject["skip_veto"]!.ToString());
            }
            if (jsonDataObject["wait_for_map"] != null)
            {
                matchConfig.WaitForMap = bool.Parse(jsonDataObject["wait_for_map"]!.ToString());
            }
            if (jsonDataObject["gamemode"] != null)
            {
                matchConfig.GameMode = jsonDataObject["gamemode"]!.ToString();
            }
            // Legacy support: convert old "wingman": true to "gamemode": "wingman"
            else if (jsonDataObject["wingman"] != null && bool.Parse(jsonDataObject["wingman"]!.ToString()))
            {
                matchConfig.GameMode = "wingman";
            }
            if (jsonDataObject["veto_mode"] != null)
            {
                matchConfig.MapBanOrder = jsonDataObject["veto_mode"]!.ToObject<List<string>>()!;
            }
            if (jsonDataObject["match_end_route"] != null)
            {
                matchConfig.MatchEndRoute = jsonDataObject["match_end_route"]!.ToString();
                Log($"[GetOptionalMatchValues] MatchEndRoute set to: '{matchConfig.MatchEndRoute}'");
            }
            if (jsonDataObject["remote_log_url"] != null)
            {
                matchConfig.RemoteLogURL = jsonDataObject["remote_log_url"]!.ToString();
                Log($"[GetOptionalMatchValues] RemoteLogURL set to: '{matchConfig.RemoteLogURL}'");
            }
            if (jsonDataObject["remote_log_header_key"] != null)
            {
                matchConfig.RemoteLogHeaderKey = jsonDataObject["remote_log_header_key"]!.ToString();
                Log($"[GetOptionalMatchValues] RemoteLogHeaderKey set to: '{matchConfig.RemoteLogHeaderKey}'");
            }
            if (jsonDataObject["remote_log_header_value"] != null)
            {
                matchConfig.RemoteLogHeaderValue = jsonDataObject["remote_log_header_value"]!.ToString();
                Log($"[GetOptionalMatchValues] RemoteLogHeaderValue set to: '{matchConfig.RemoteLogHeaderValue}'");
            }
        }

        public void HandleTeamNameChangeCommand(CCSPlayerController? player, string teamName, int teamNum) {
            if (!IsPlayerAdmin(player, "css_team", "@css/config")) {
                SendPlayerNotAdminMessage(player);
                return;
            }
            if (matchStarted) {
                // ReplyToUserCommand(player, "Team names cannot be changed once the match is started!");
                ReplyToUserCommand(player, Localizer["squidcup.mm.teamcannotbechanged"]);
                return;
            }
            teamName = RemoveSpecialCharacters(teamName.Trim());
            if (teamName == "") {
                // ReplyToUserCommand(player, $"Usage: !team{teamNum} <name>");
                ReplyToUserCommand(player, Localizer["squidcup.cc.usage", $"!team{teamNum} <name>"]);
            }

            if (teamNum == 1) {
                squidcupTeam1.teamName = teamName;
                teamSides[squidcupTeam1] = "CT";
                reverseTeamSides["CT"] = squidcupTeam1;
                foreach (var coach in squidcupTeam1.coach)
                {
                    coach.Clan = $"[{squidcupTeam1.teamName} COACH]";
                }
            } else if (teamNum == 2) {
                squidcupTeam2.teamName = teamName;
                teamSides[squidcupTeam2] = "TERRORIST";
                reverseTeamSides["TERRORIST"] = squidcupTeam2;
                foreach (var coach in squidcupTeam2.coach)
                {
                    coach.Clan = $"[{squidcupTeam2.teamName} COACH]";
                }
            }
            Server.ExecuteCommand($"mp_teamname_{teamNum} {teamName};");
        }

        public void SwapSidesInTeamData(bool swapTeams) {
            // if (swapTeams) {
            //     // Here, we sync squidcupTeam1 and squidcupTeam2 with the actual team1 and team2
            //     (squidcupTeam2, squidcupTeam1) = (squidcupTeam1, squidcupTeam2);
            // }

            (teamSides[squidcupTeam1], teamSides[squidcupTeam2]) = (teamSides[squidcupTeam2], teamSides[squidcupTeam1]);
            (reverseTeamSides["CT"], reverseTeamSides["TERRORIST"]) = (reverseTeamSides["TERRORIST"], reverseTeamSides["CT"]);
        }

        private CsTeam GetPlayerTeam(CCSPlayerController player)
        {
            CsTeam playerTeam = CsTeam.None;
            var steamId = player.SteamID;
            try
            {
                if (squidcupTeam1.teamPlayers != null && squidcupTeam1.teamPlayers[steamId.ToString()] != null)
                {
                    if (teamSides[squidcupTeam1] == "CT")
                    {
                        playerTeam = CsTeam.CounterTerrorist;
                    }
                    else if (teamSides[squidcupTeam1] == "TERRORIST")
                    {
                        playerTeam = CsTeam.Terrorist;
                    }

                }
                else if (squidcupTeam2.teamPlayers != null && squidcupTeam2.teamPlayers[steamId.ToString()] != null)
                {
                    if (teamSides[squidcupTeam2] == "CT")
                    {
                        playerTeam = CsTeam.CounterTerrorist;
                    }
                    else if (teamSides[squidcupTeam2] == "TERRORIST")
                    {
                        playerTeam = CsTeam.Terrorist;
                    }
                }
                else if (matchConfig.Spectators != null && matchConfig.Spectators[steamId.ToString()] != null)
                {
                    playerTeam = CsTeam.Spectator;
                }
            }
            catch (Exception ex)
            {
                Log($"[GetPlayerTeam - FATAL] Exception occurred: {ex.Message}");
            }
            return playerTeam;
        }

        public void EndSeries(string? winnerName, int restartDelay, int t1score, int t2score)
        {
            long matchId = liveMatchId;
            if (winnerName == null)
            {
                PrintToAllChat($"{ChatColors.Green}{squidcupTeam1.teamName}{ChatColors.Default} and {ChatColors.Green}{squidcupTeam2.teamName}{ChatColors.Default} have tied the match");
            }
            else
            {
                Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{winnerName}{ChatColors.Default} has won the match");
            }

            string winnerTeam = (winnerName == null) ? "none" : t1score > t2score ? "team1" : "team2";

            var seriesResultEvent = new SquidcupSeriesResultEvent()
            {
                MatchId = matchId,
                Winner = new Winner(t1score > t2score && reverseTeamSides["CT"] == squidcupTeam1 ? "3" : "2", winnerTeam),
                Team1SeriesScore = t1score > t2score ? 1 : 0,
                Team2SeriesScore = t2score > t1score ? 1 : 0,
                TimeUntilRestore = 10,
            };

            Task.Run(async () => {
                try
                {
                    Log($"[EndSeries] Starting match end operations for matchId: {matchId}");
                    
                    try
                    {
                        await database.SetMatchEndData(matchId, winnerName ?? "Draw", t1score > t2score ? 1 : 0, t2score > t1score ? 1 : 0);
                        Log($"[EndSeries] Successfully set match end data for matchId: {matchId}");
                    }
                    catch (Exception ex)
                    {
                        Log($"[EndSeries - ERROR] Failed to set match end data for matchId: {matchId} [ERROR]: {ex.Message}");
                    }
                    
                    try
                    {
                        // Making sure that map end event is fired first
                        await Task.Delay(2000);
                        Log($"[EndSeries] Sending series result event for matchId: {matchId}");
                        await SendEventAsync(seriesResultEvent);
                        Log($"[EndSeries] Successfully sent series result event for matchId: {matchId}");
                    }
                    catch (Exception ex)
                    {
                        Log($"[EndSeries - ERROR] Failed to send series result event for matchId: {matchId} [ERROR]: {ex.Message}");
                    }
                    
                    try
                    {
                        // Send match end notification to MatchEndRoute
                        Log($"[EndSeries] Sending match end notification for matchId: {matchId}");
                        await SendMatchEndNotificationAsync(matchId);
                        Log($"[EndSeries] Successfully sent match end notification for matchId: {matchId}");
                    }
                    catch (Exception ex)
                    {
                        Log($"[EndSeries - ERROR] Failed to send match end notification for matchId: {matchId} [ERROR]: {ex.Message}");
                        Log($"[EndSeries - ERROR] Exception details: {ex}");
                    }
                    
                    Log($"[EndSeries] Completed all match end operations for matchId: {matchId}");
                }
                catch (Exception ex)
                {
                    Log($"[EndSeries - FATAL] Unexpected error in match end operations for matchId: {matchId} [ERROR]: {ex.Message}");
                    Log($"[EndSeries - FATAL] Exception details: {ex}");
                }
            });

            if (resetCvarsOnSeriesEnd) ResetChangedConvars();
            isMatchLive = false;
            
            Log($"[EndSeries] Scheduling ResetMatch call in {restartDelay} seconds for matchId: {matchId}");
            AddTimer(restartDelay, () => {
                try 
                {
                    Log($"[EndSeries] Executing delayed ResetMatch for matchId: {matchId}");
                    ResetMatch(false);
                    Log($"[EndSeries] Successfully completed ResetMatch for matchId: {matchId}. isMatchSetup is now: {isMatchSetup}");
                }
                catch (Exception ex)
                {
                    Log($"[EndSeries - FATAL] Error in delayed ResetMatch for matchId: {matchId} [ERROR]: {ex.Message}");
                    Log($"[EndSeries - FATAL] Stack trace: {ex.StackTrace}");
                }
            });
        }

        public void HandlePlayoutConfig()
        {
            if (isPlayOutEnabled) {
                Server.ExecuteCommand("mp_overtime_enable 0");
                Server.ExecuteCommand("mp_match_can_clinch false");
            } else {
                var absoluteCfgPath = Path.Join(Server.GameDirectory + "/csgo/cfg", GetLiveCfgPath(matchConfig.GameMode));
                string? matchCanClinch = GetConvarValueFromCFGFile(absoluteCfgPath, "mp_match_can_clinch");
                string? overtimeEnabled = GetConvarValueFromCFGFile(absoluteCfgPath, "mp_overtime_enable");
                Server.ExecuteCommand($"mp_match_can_clinch {matchCanClinch ?? "1"}");
                Server.ExecuteCommand($"mp_overtime_enable {overtimeEnabled ?? "1"}");
            }
        }

    }
}
