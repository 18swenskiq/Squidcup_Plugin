using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using System;

namespace Squidcup
{
    public partial class Squidcup
    {
        public bool isPreVeto = false;
        public bool isVeto = false;
        public int warningsPrinted = 0;
        public int vetoCountdownTime = 5; // In Seconds

        public bool mapChangePending = false;
        public CounterStrikeSharp.API.Modules.Timers.Timer? vetoStateTimer = null;
        public Dictionary<string, int> vetoCaptains = new(){
            {"team1", -1},
            {"team2", -1}
        };

        public CsTeam lastVetoTeam = CsTeam.None;

        public void CreateVeto()
        {
            SwapPlayersToTeams();
            vetoCaptains["team1"] = GetTeamCaptain("team1");
            vetoCaptains["team2"] = GetTeamCaptain("team2");
            // Todo: Implement pauseOnVeto CVAR
            // if (pauseOnVeto) {
            //     Server.ExecuteCommand("mp_pause_match");
            //     isPaused = true;
            //     unpauseData["pauseTeam"] = "Admin";
            // }
            Server.ExecuteCommand("mp_warmup_end");
            Server.ExecuteCommand("mp_pause_match");
            isPaused = true;
            unpauseData["pauseTeam"] = "Admin";
            vetoStateTimer = AddTimer(1, VetoCountdown, TimerFlags.REPEAT);
            isVeto = true;
            readyAvailable = false;
            isWarmup = false;
            KillPhaseTimers();
        }

        public void VetoCountdown()
        {
            if (!isVeto)
            {
                warningsPrinted = 0;
                vetoStateTimer?.Kill();
                vetoStateTimer = null;
                return;
            }
            if (warningsPrinted >= vetoCountdownTime)
            {
                int team1Captain = vetoCaptains["team1"];
                int team2Captain = vetoCaptains["team2"];
                warningsPrinted = 0;
                if (!playerData.ContainsKey(team1Captain) || !playerData.ContainsKey(team2Captain) || !playerData[team1Captain].IsValid || !playerData[team2Captain].IsValid)
                {
                    AbortVeto();
                    vetoStateTimer?.Kill();
                    vetoStateTimer = null;
                    return;
                }
                Server.PrintToChatAll($"{chatPrefix} Captain for {ChatColors.Green}{squidcupTeam1.teamName}{ChatColors.Default}: {ChatColors.Green}{playerData[team1Captain].PlayerName}{ChatColors.Default}");
                Server.PrintToChatAll($"{chatPrefix} Captain for {ChatColors.Green}{squidcupTeam2.teamName}{ChatColors.Default}: {ChatColors.Green}{playerData[team2Captain].PlayerName}{ChatColors.Default}");

                HandleVetoStep();
                vetoStateTimer?.Kill();
                vetoStateTimer = null;
                return;
            }
            warningsPrinted++;
            int secondsRemaining = vetoCountdownTime - warningsPrinted + 1;
            Server.PrintToChatAll($"{chatPrefix} Map selection commencing in {secondsRemaining}");
        }

        public void HandleVetoStep()
        {
            // As long as sides are not set for a map, either give side pick or auto-decide sides and recursively call this.
            if (matchConfig.MapSides.Count < matchConfig.Maplist.Count)
            {
                if (matchConfig.MatchSideType == "standard")
                {
                    CsTeam otherMatchTeam = lastVetoTeam;
                    if (lastVetoTeam == CsTeam.Terrorist) otherMatchTeam = CsTeam.CounterTerrorist;
                    else if (lastVetoTeam == CsTeam.CounterTerrorist) otherMatchTeam = CsTeam.Terrorist;
                    PromptForSideSelectionInChat(otherMatchTeam);
                }
                else
                {
                    HandleAutomaticSideSelection();
                    HandleVetoStep();
                }
            }
            else if (matchConfig.Maplist.Count > 1)
            {
                Log($"[HANDLEMAPRESTRICTIONS] Multiple maps specified in maplist for BO1. Using first map: {matchConfig.Maplist[0]}");
                matchConfig.Maplist = new List<string> { matchConfig.Maplist[0] };
            }
            else
            {
                if (matchConfig.MapsLeftInVetoPool.Count == 1)
                {
                    // Only 1 map left in the pool, add it by deduction and determine knife logic.
                    string mapName = matchConfig.MapsLeftInVetoPool[0];
                    PickMap(mapName, 0);
                    HandleAutomaticSideSelection();
                    FinishVeto();
                }
                else
                {
                    // More than 1 map in the pool and not all maps are picked; present choices as determine by config.
                    PromptForMapSelectionInChat(GetCurrentMapSelectionOption());
                }
            }
        }

        public void PromptForMapSelectionInChat(string option) {
            string action = "";
            int client = -1;
            string stepMessage = "";
            switch (option) 
            {
                case "team1_ban":
                    action = $"{ChatColors.Green}{squidcupTeam1.teamName}{ChatColors.Default} must now {ChatColors.Red}BAN{ChatColors.Default} a map.";
                    client = vetoCaptains["team1"];
                    stepMessage = $"Use .ban <map> to ban a map";
                    break;
                case "team2_ban":
                    action = $"{ChatColors.Green}{squidcupTeam2.teamName}{ChatColors.Default} must now {ChatColors.Red}BAN{ChatColors.Default} a map.";
                    client = vetoCaptains["team2"];
                    stepMessage = $"Use .ban <map> to ban a map";
                    break;                                                       
                case "team1_pick":
                    action = $"{ChatColors.Green}{squidcupTeam1.teamName}{ChatColors.Default} must now {ChatColors.Green}PICK{ChatColors.Default} a map to play as map {matchConfig.Maplist.Count + 1}.";
                    client = vetoCaptains["team1"];
                    stepMessage = $"Use .pick <map> to pick a map.";
                    break;
                case "team2_pick":
                    action = $"{ChatColors.Green}{squidcupTeam2.teamName}{ChatColors.Default} must now {ChatColors.Green}PICK{ChatColors.Default} a map to play as map {matchConfig.Maplist.Count + 1}.";
                    client = vetoCaptains["team2"];
                    stepMessage = $"Use .pick <map> to pick a map.";
                    break;
            }
            if (!playerData.ContainsKey(client) || !playerData[client].IsValid)
            {
                Log($"[PromptForMapSelectionInChat] Invalid captain found with ID: {client}");
                return;
            }
            Server.PrintToChatAll($"{chatPrefix} {action}");

            string mapListAsString = string.Join(", ", matchConfig.MapsLeftInVetoPool);
            Server.PrintToChatAll($"{chatPrefix} Remaining Maps: {mapListAsString}");

            playerData[client].PrintToChat($"{chatPrefix} {stepMessage}");
        }

        [ConsoleCommand("css_pick", "Picks map")]
        public void OnPickMapCommand(CCSPlayerController? player, CommandInfo? command) {
            if (player == null || command == null) return;
            if (command.ArgCount < 1) return;
            string mapArg = command.ArgByIndex(1);
            HandeMapPickCommand(player, mapArg);
        }

        [ConsoleCommand("css_ban", "Bans map")]
        public void OnBanMapCommand(CCSPlayerController? player, CommandInfo? command) { 
            if (player == null || command == null) return;
            if (command.ArgCount < 1) return;
            string mapArg = command.ArgByIndex(1);
            HandeMapBanCommand(player, mapArg);
        }

        public void HandeMapBanCommand(CCSPlayerController player, string map)
        {
            if (!isVeto || SidePickPending() || player == null || map == null) return;

            int playerTeam = player.TeamNum;
            string currentTeamToBan;
            switch (GetCurrentMapSelectionOption()) {
                case "team1_ban":
                    currentTeamToBan = "team1";
                    break;
                case "team2_ban":
                    currentTeamToBan = "team2";
                    break;
                case "invalid":
                case "team1_pick":
                case "team2_pick":
                default:
                    return;
            }

            if (player.UserId != vetoCaptains[currentTeamToBan]) return;

            if (!BanMap(map, playerTeam)) {
                PrintToPlayerChat(player, $"{map} is not a valid map.");
            } else {
                HandleVetoStep();
            }
        }

        public void HandeMapPickCommand(CCSPlayerController player, string map)
        {
            if (!isVeto || SidePickPending() || player == null || map == null) return;

            int playerTeam = player.TeamNum;
            string currentTeamToPick;
            switch (GetCurrentMapSelectionOption()) 
            {
                case "team1_pick":
                    currentTeamToPick = "team1";
                    break;
                case "team2_pick":
                    currentTeamToPick = "team2";
                    break;
                case "invalid":
                case "team1_ban":
                case "team2_ban":
                default:
                    return;
            }

            if (player.UserId != vetoCaptains[currentTeamToPick]) return;

            if (!PickMap(map, playerTeam)) {
                PrintToPlayerChat(player, $"{map} is not a valid map.");
            } else {
                HandleVetoStep();
            }
        }


        public bool PickMap(string mapName, int team) {
            (bool mapRemoved, string mapRemovedName) = RemoveMapFromMapPool(mapName);

            if (!mapRemoved) return false;

            Team squidcupTeam = squidcupTeam1;

            if (team != 0) {
                squidcupTeam = (team == 2) ? reverseTeamSides["TERRORIST"] : reverseTeamSides["CT"];
                Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{squidcupTeam.teamName}{ChatColors.Default} picked {ChatColors.Green}{mapRemovedName}{ChatColors.Default} as map {matchConfig.Maplist.Count + 1}");
            }

            matchConfig.Maplist.Add(mapRemovedName);

            var mapPickedEvent = new SquidcupMapPickedEvent
            {
                MatchId = liveMatchId,
                MapName = mapRemovedName,
                MapNumber = matchConfig.Maplist.Count,
                Team = (squidcupTeam == squidcupTeam1) ? "team1" : "team2",
            };

            Task.Run(async () => {
                await SendEventAsync(mapPickedEvent);
            });

            lastVetoTeam = (CsTeam)team;

            return true;
        }

        public bool BanMap(string mapName, int team) 
        {
            (bool mapRemoved, string mapRemovedName) = RemoveMapFromMapPool(mapName);

            if (!mapRemoved) return false;

            Team squidcupTeam = squidcupTeam1;

            if (team != 0) {
                squidcupTeam = (team == 2) ? reverseTeamSides["TERRORIST"] : reverseTeamSides["CT"];
                Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{squidcupTeam.teamName}{ChatColors.Default} banned {ChatColors.LightRed}{mapRemovedName}{ChatColors.Default}");
            }

            var mapMapVetoedEvent = new SquidcupMapVetoedEvent
            {
                MatchId = liveMatchId,
                MapName = mapRemovedName,
                Team = (squidcupTeam == squidcupTeam1) ? "team1" : "team2",
            };

            Task.Run(async () => {
                await SendEventAsync(mapMapVetoedEvent);
            });

            lastVetoTeam = (CsTeam)team;

            return true;
        }

        public void AbortVeto()
        {
            // Todo: Add AbortVeto() when captain is disconnecting in-between veto
            Server.PrintToChatAll($"{chatPrefix} A team captain left during map selection. Map selection is paused.");
            Server.PrintToChatAll($"{chatPrefix} Type .ready when you are ready to resume map selection.");
            isPreVeto = true;
            isVeto = false;
            if (isPaused)
            {
                UnpauseMatch();
            }
            vetoCaptains = new(){
                {"team1", -1},
                {"team2", -1}
            };
            foreach (var key in playerReadyStatus.Keys) {
                playerReadyStatus[key] = false;
            }
            readyAvailable = true;
            isWarmup = true;
            StartWarmup();
        }

        public void FinishVeto() 
        {
            Server.PrintToChatAll($"{chatPrefix} The maps have been decided:");
            matchConfig.MapsLeftInVetoPool.Clear();

            if (isPaused) {
                UnpauseMatch();
            }

            // If a team has a map advantage, don't print that map.
            int mapNumber = matchConfig.CurrentMapNumber;

            for (int i = mapNumber; i < matchConfig.Maplist.Count; i++) {
                Server.PrintToChatAll($"{chatPrefix} Map {i + 1 - mapNumber}: {matchConfig.Maplist[i]}.");
            }

            string currentMapName = Server.MapName;
            string mapToPlay = matchConfig.Maplist[0];

            // In case the sides don't match after selection, we check it here before writing the backup.
            // Also required if the map doesn't need to change.
            SetMapSides();
            ExecuteChangedConvars();
            foreach (var key in playerReadyStatus.Keys) {
                playerReadyStatus[key] = false;
            }

            if (IsMapReloadRequiredForGameMode(matchConfig.Wingman) || mapReloadRequired || currentMapName != mapToPlay) {

                SetCorrectGameMode();
                float delay = 7.0f;
                mapChangePending = true;
                // Todo: Implement displayGotvVeto cvar
                // if (displayGotvVeto) {
                //     delay += GetTvDelay();
                // }
                AddTimer(delay, () => {
                    string nextMap = matchConfig.Maplist[matchConfig.CurrentMapNumber];
                    ChangeMap(nextMap, 3);
                });
            }
            isWarmup = true;
            readyAvailable = true;
            isPreVeto = false;
            isVeto = false;
            StartWarmup();
        }


        public int GetTeamCaptain(string team)
        {
            Team squidcupTeam = team == "team1" ? squidcupTeam1 : squidcupTeam2;
            int teamSide = teamSides[squidcupTeam] == "CT" ? 3 : 2;
            foreach (var key in playerData.Keys)
            {
                if (!playerData[key].IsValid || playerData[key].IsBot) continue;
                if (playerData[key].TeamNum == teamSide) return key;
            }

            return -1;
        }

        public void SwapPlayersToTeams()
        {
            foreach (var key in playerData.Keys)
            {
                if (!playerData[key].IsValid || playerData[key].IsBot) continue;
                playerData[key].SwitchTeam(GetPlayerTeam(playerData[key]));
            }
        }

        public string GetCurrentMapSelectionOption()
        {
            // Number of banned maps must be: original pool - (current pool + picked);
            // 7 - (4 + 2) = 1; if 4 are left and 2 were picked, 1 must have been banned.
            int mapsBanned = matchConfig.MapsPool.Count - (matchConfig.MapsLeftInVetoPool.Count + matchConfig.Maplist.Count);
            int index = matchConfig.Maplist.Count + mapsBanned;
            if (index > matchConfig.MapBanOrder.Count - 1)
            {
                return "invalid";
            }
            return matchConfig.MapBanOrder[index];
        }
        public bool SidePickPending() {
            return matchConfig.MapSides.Count < matchConfig.Maplist.Count && matchConfig.MatchSideType == "standard";
        }

        public void HandleAutomaticSideSelection() {
            if (matchConfig.MatchSideType == "random") {
                matchConfig.MapSides.Add(new Random().Next(0, 2) == 0 ? "team1_ct" : "team1_t");
            } else {
                matchConfig.MapSides.Add(matchConfig.MatchSideType == "never_knife" ? "team1_ct" : "knife");
            }
        }

        public (bool, string) RemoveMapFromMapPool(string mapName) {
            string mapRemoved = "";
            int eraseIndex = -1;
            // First check if we have a single match with a substring.
            if (mapName.Length >= 4) {
                for (int i = 0; i < matchConfig.MapsLeftInVetoPool.Count; i++) {
                    mapRemoved = matchConfig.MapsLeftInVetoPool[i];
                    if (mapRemoved.IndexOf(mapName, StringComparison.OrdinalIgnoreCase) > -1) {
                        if (eraseIndex >= 0) {
                            eraseIndex = -1;  // If more than one match, reset and break.
                            break;
                        }
                        eraseIndex = i;
                    }
                }
            }
            // If no match or more than one match on substring, restart, this time only matching the full string.
            if (eraseIndex == -1) {
                for (int i = 0; i < matchConfig.MapsLeftInVetoPool.Count; i++) {
                    mapRemoved = matchConfig.MapsLeftInVetoPool[i];
                    if (mapRemoved == mapName) {
                        eraseIndex = i;
                        break;
                    }
                }
            }
            if (eraseIndex >= 0) {
                mapRemoved = matchConfig.MapsLeftInVetoPool[eraseIndex];
                matchConfig.MapsLeftInVetoPool.RemoveAt(eraseIndex);
                return (true, mapRemoved);
            }
            if (mapName.IndexOf("cobble", StringComparison.OrdinalIgnoreCase) > -1) {
                // Because Cobblestone is the only map that's actually misspelled, we re-run the code if the input contained
                // "cobble" but there was no match, this time using "cbble" instead, which will match de_cbble.
                return RemoveMapFromMapPool("cbble");
            }
            return (false, mapRemoved);
        }
        public void PromptForSideSelectionInChat(CsTeam team) {
            string mapName = matchConfig.Maplist[^1];
            Team squidcupTeam = (team == CsTeam.CounterTerrorist) ? reverseTeamSides["CT"] : reverseTeamSides["TERRORIST"];
            string teamString = (squidcupTeam == squidcupTeam1) ? "team1" : "team2";
            
            Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{squidcupTeam.teamName}{ChatColors.Default} must now pick a side to play on {ChatColors.Green}{mapName}{ChatColors.Default}");

            int client = vetoCaptains[teamString];
            if (!playerData.ContainsKey(client) || !playerData[client].IsValid) return;

            playerData[client].PrintToChat($"{chatPrefix} Use .ct or .t to pick a side");
        }

        public bool ValidateMapBanLogic() 
        {
            // For BO1, the logic is simple: we need exactly (map pool size - 1) ban operations
            // to leave one map remaining for play
            if (matchConfig.MapsPool.Count <= 1) {
                Log($"[ValidateMapBanLogic] Map pool must contain at least 2 maps for veto process.");
                return false;
            }

            int expectedBanCount = matchConfig.MapsPool.Count - 1;
            if (matchConfig.MapBanOrder.Count != expectedBanCount) {
                Log($"[ValidateMapBanLogic] For BO1 with {matchConfig.MapsPool.Count} maps in pool, expected {expectedBanCount} ban operations but found {matchConfig.MapBanOrder.Count}.");
                return false;
            }

            return true;
        }

        public void HandleSideChoice(CsTeam side, int client) {
            if (!SidePickPending()) {
                // No side selection is done by players in this case.
                return;
            }
            Team team = squidcupTeam1;

            if (lastVetoTeam == CsTeam.Terrorist) team = reverseTeamSides["CT"];
            else if (lastVetoTeam == CsTeam.CounterTerrorist) team = reverseTeamSides["TERRORIST"];

            string pickingTeam = (team == squidcupTeam1) ? "team1" : "team2";

            if (client != vetoCaptains[pickingTeam]) {
                // Only captain can select a side.
                return;
            }
            PickSide(side, pickingTeam);
            HandleVetoStep();
        }

        public void PickSide(CsTeam side, string team) {
            if (side == CsTeam.CounterTerrorist) {
                matchConfig.MapSides.Add(team == "team1" ? "team1_ct" : "team1_t");
            } else {
                matchConfig.MapSides.Add(team == "team1" ? "team1_t" : "team1_ct");
            }

            int mapNumber = matchConfig.Maplist.Count - 1;

            string mapName = matchConfig.Maplist[mapNumber];

            string sideFormatted = (side == CsTeam.CounterTerrorist) ? "CT" : "T";

            Team squidcupTeam = (team == "team1") ? squidcupTeam1 : squidcupTeam2;

            Server.PrintToChatAll($"{chatPrefix} {ChatColors.Green}{squidcupTeam.teamName}{ChatColors.Default} elected to start as {ChatColors.Green}{sideFormatted}{ChatColors.Default} on {ChatColors.Green}{mapName}{ChatColors.Default}.");

            var sidePickedEvent = new SquidcupSidePickedEvent
            {
                MatchId = liveMatchId,
                MapName = mapName,
                MapNumber = matchConfig.Maplist.Count,
                Team = (squidcupTeam == squidcupTeam1) ? "team1" : "team2",
                Side = sideFormatted.ToLower()
            };
            Task.Run(async () => {
                await SendEventAsync(sidePickedEvent);
            });
        }

        public void GenerateDefaultVetoSetup()
        {
            Team startingVetoTeam = squidcupTeam1;
            if (lastVetoTeam == CsTeam.CounterTerrorist)
            {
                if (reverseTeamSides["CT"] == squidcupTeam1) startingVetoTeam = squidcupTeam2;
                if (reverseTeamSides["CT"] == squidcupTeam2) startingVetoTeam = squidcupTeam1;
            }
            else if (lastVetoTeam == CsTeam.Terrorist)
            {
                if (reverseTeamSides["TERRORIST"] == squidcupTeam1) startingVetoTeam = squidcupTeam2;
                if (reverseTeamSides["TERRORIST"] == squidcupTeam2) startingVetoTeam = squidcupTeam1;
            }

            // For BO1: alternate bans until only one map remains
            int numberOfBans = matchConfig.MapsPool.Count - 1;
            for (int i = 0; i < numberOfBans; i++)
            {
                matchConfig.MapBanOrder.Add(
                    i % 2 == 0
                        ? (startingVetoTeam == squidcupTeam1 ? "team1_ban" : "team2_ban")
                        : (startingVetoTeam == squidcupTeam1 ? "team2_ban" : "team1_ban"));
            }
        }

        public void SkipVeto()
        {
            isWarmup = true;
            readyAvailable = true;
            isPreVeto = false;
            isVeto = false;
            StartWarmup();
        }
    }
}

