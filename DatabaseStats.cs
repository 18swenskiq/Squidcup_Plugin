using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;

namespace Squidcup
{
    public class StatsApiClient
    {
        private readonly HttpClient httpClient;
        private string apiBaseUrl = "";
        private string pendingQueuePath = "";
        private readonly object queueLock = new();

        public StatsApiClient()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public void Initialize(string moduleDirectory)
        {
            LoadConfig(moduleDirectory);
            pendingQueuePath = Path.Join(moduleDirectory, "pending_stats.json");
            
            // Process any pending requests from previous session
            _ = Task.Run(ProcessPendingQueueAsync);
        }

        private void LoadConfig(string moduleDirectory)
        {
            string configFile = Path.Combine(Server.GameDirectory + "/csgo/cfg/Squidcup", "database.json");
            
            if (!File.Exists(configFile))
            {
                // Create default config
                var defaultConfig = new ApiConfig { ApiBaseUrl = "https://your-api.example.com" };
                string json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFile, json);
                Log($"[Initialize] Default config created at: {configFile}");
            }

            try
            {
                string jsonContent = File.ReadAllText(configFile);
                var config = JsonSerializer.Deserialize<ApiConfig>(jsonContent);
                if (config?.ApiBaseUrl != null)
                {
                    apiBaseUrl = config.ApiBaseUrl.TrimEnd('/');
                    Log($"[Initialize] API Base URL: {apiBaseUrl}");
                }
                else
                {
                    Log("[Initialize - ERROR] ApiBaseUrl not found in config");
                }
            }
            catch (Exception ex)
            {
                Log($"[Initialize - ERROR] Failed to load config: {ex.Message}");
            }
        }

        #region API Methods

        public long InitMatch(string team1Name, string team2Name, string serverIp, bool isMatchSetup, long liveMatchId, int mapNumber, string seriesType)
        {
            string mapName = Server.MapName;
            
            var request = new InitMatchRequest
            {
                MatchId = liveMatchId != -1 ? liveMatchId : null,
                Team1Name = team1Name,
                Team2Name = team2Name,
                ServerIp = serverIp,
                SeriesType = seriesType,
                MapNumber = mapNumber,
                MapName = mapName
            };

            var response = SendRequestWithRetry<InitMatchResponse>("POST", "/api/matches", request);
            
            if (response?.MatchId != null)
            {
                Log($"[InitMatch] Match initialized with ID: {response.MatchId}");
                return response.MatchId.Value;
            }
            
            Log($"[InitMatch] Failed to get match ID from API, using provided ID: {liveMatchId}");
            return liveMatchId;
        }

        public void UpdateTeamData(int matchId, string team1Name, string team2Name)
        {
            // Team data is set during InitMatch, no separate endpoint needed
            Log($"[UpdateTeamData] Team data update requested for matchId: {matchId} - handled by InitMatch");
        }

        public async Task UpdatePlayerStatsAsync(long matchId, int mapNumber, Dictionary<ulong, Dictionary<string, object>> playerStatsDictionary)
        {
            var players = new List<PlayerStatsUpdate>();
            
            foreach (var kvp in playerStatsDictionary)
            {
                var stats = kvp.Value;
                players.Add(new PlayerStatsUpdate
                {
                    SteamId64 = (long)kvp.Key,
                    Team = stats["TeamName"]?.ToString() ?? "",
                    Name = stats["PlayerName"]?.ToString() ?? "",
                    Kills = Convert.ToInt32(stats["Kills"]),
                    Deaths = Convert.ToInt32(stats["Deaths"]),
                    Damage = Convert.ToInt32(stats["Damage"]),
                    Assists = Convert.ToInt32(stats["Assists"]),
                    Enemy5ks = Convert.ToInt32(stats["Enemy5Ks"]),
                    Enemy4ks = Convert.ToInt32(stats["Enemy4Ks"]),
                    Enemy3ks = Convert.ToInt32(stats["Enemy3Ks"]),
                    Enemy2ks = Convert.ToInt32(stats["Enemy2Ks"]),
                    UtilityCount = Convert.ToInt32(stats["UtilityCount"]),
                    UtilityDamage = Convert.ToInt32(stats["UtilityDamage"]),
                    UtilitySuccesses = Convert.ToInt32(stats["UtilitySuccess"]),
                    UtilityEnemies = Convert.ToInt32(stats["UtilityEnemies"]),
                    FlashCount = Convert.ToInt32(stats["FlashCount"]),
                    FlashSuccesses = Convert.ToInt32(stats["FlashSuccess"]),
                    HealthPointsRemovedTotal = Convert.ToInt32(stats["HealthPointsRemovedTotal"]),
                    HealthPointsDealtTotal = Convert.ToInt32(stats["HealthPointsDealtTotal"]),
                    ShotsFiredTotal = Convert.ToInt32(stats["ShotsFiredTotal"]),
                    ShotsOnTargetTotal = Convert.ToInt32(stats["ShotsOnTargetTotal"]),
                    V1Count = Convert.ToInt32(stats["1v1Count"]),
                    V1Wins = Convert.ToInt32(stats["1v1Wins"]),
                    V2Count = Convert.ToInt32(stats["1v2Count"]),
                    V2Wins = Convert.ToInt32(stats["1v2Wins"]),
                    EntryCount = Convert.ToInt32(stats["EntryCount"]),
                    EntryWins = Convert.ToInt32(stats["EntryWins"]),
                    EquipmentValue = Convert.ToInt32(stats["EquipmentValue"]),
                    MoneySaved = Convert.ToInt32(stats["MoneySaved"]),
                    KillReward = Convert.ToInt32(stats["KillReward"]),
                    LiveTime = Convert.ToInt32(stats["LiveTime"]),
                    HeadShotKills = Convert.ToInt32(stats["HeadShotKills"]),
                    CashEarned = Convert.ToInt32(stats["CashEarned"]),
                    EnemiesFlashed = Convert.ToInt32(stats["EnemiesFlashed"])
                });
            }

            var request = new UpdatePlayersRequest { Players = players };
            string endpoint = $"/api/matches/{matchId}/maps/{mapNumber}/players";
            
            await Task.Run(() => SendRequestWithRetry<ApiResponse>("PUT", endpoint, request));
            Log($"[UpdatePlayerStatsAsync] Updated stats for {players.Count} players in match {matchId}");
        }

        public async Task UpdateMapStatsAsync(long matchId, int mapNumber, int t1Score, int t2Score)
        {
            var request = new UpdateMapScoresRequest
            {
                Team1Score = t1Score,
                Team2Score = t2Score
            };

            string endpoint = $"/api/matches/{matchId}/maps/{mapNumber}/scores";
            await Task.Run(() => SendRequestWithRetry<ApiResponse>("PUT", endpoint, request));
            Log($"[UpdateMapStatsAsync] Updated scores for match {matchId} map {mapNumber}: {t1Score}-{t2Score}");
        }

        public async Task SetMapEndData(long matchId, int mapNumber, string winnerName, int t1Score, int t2Score, int team1SeriesScore, int team2SeriesScore)
        {
            var request = new EndMapRequest
            {
                WinnerName = winnerName,
                Team1Score = t1Score,
                Team2Score = t2Score,
                Team1SeriesScore = team1SeriesScore,
                Team2SeriesScore = team2SeriesScore
            };

            string endpoint = $"/api/matches/{matchId}/maps/{mapNumber}/end";
            await Task.Run(() => SendRequestWithRetry<ApiResponse>("POST", endpoint, request));
            Log($"[SetMapEndData] Finalized map {mapNumber} for match {matchId}, winner: {winnerName}");
        }

        public async Task SetMatchEndData(long matchId, string winnerName, int t1Score, int t2Score)
        {
            var request = new EndMatchRequest
            {
                WinnerName = winnerName,
                Team1Score = t1Score,
                Team2Score = t2Score
            };

            string endpoint = $"/api/matches/{matchId}/end";
            await Task.Run(() => SendRequestWithRetry<ApiResponse>("POST", endpoint, request));
            Log($"[SetMatchEndData] Finalized match {matchId}, winner: {winnerName}");
        }

        #endregion

        #region HTTP Client with Retry

        private T? SendRequestWithRetry<T>(string method, string endpoint, object payload) where T : class
        {
            int maxRetries = 3;
            int[] delaysMs = { 1000, 2000, 4000 }; // Exponential backoff

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var result = SendRequest<T>(method, endpoint, payload);
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[SendRequest] Attempt {attempt + 1}/{maxRetries} failed for {method} {endpoint}: {ex.Message}");
                }

                if (attempt < maxRetries - 1)
                {
                    Thread.Sleep(delaysMs[attempt]);
                }
            }

            // All retries failed, queue for later
            Log($"[SendRequest] All retries failed, queueing request: {method} {endpoint}");
            QueueFailedRequest(method, endpoint, payload);
            return null;
        }

        private T? SendRequest<T>(string method, string endpoint, object payload) where T : class
        {
            string url = apiBaseUrl + endpoint;
            string jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });

            using var request = new HttpRequestMessage(new HttpMethod(method), url);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = httpClient.Send(request);
            string responseBody = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();

            if (response.IsSuccessStatusCode)
            {
                if (typeof(T) == typeof(ApiResponse))
                {
                    return new ApiResponse { Success = true } as T;
                }
                return JsonSerializer.Deserialize<T>(responseBody, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
            }

            Log($"[SendRequest] HTTP {(int)response.StatusCode} for {method} {endpoint}: {responseBody}");
            throw new HttpRequestException($"HTTP {response.StatusCode}");
        }

        #endregion

        #region Request Queue

        private void QueueFailedRequest(string method, string endpoint, object payload)
        {
            try
            {
                lock (queueLock)
                {
                    var queue = LoadQueue();
                    queue.Add(new QueuedRequest
                    {
                        Method = method,
                        Endpoint = endpoint,
                        Payload = JsonSerializer.Serialize(payload),
                        Timestamp = DateTime.UtcNow
                    });
                    SaveQueue(queue);
                }
                Log($"[Queue] Request queued. Queue size: {LoadQueue().Count}");
            }
            catch (Exception ex)
            {
                Log($"[Queue - ERROR] Failed to queue request: {ex.Message}");
            }
        }

        private List<QueuedRequest> LoadQueue()
        {
            if (!File.Exists(pendingQueuePath))
            {
                return new List<QueuedRequest>();
            }

            try
            {
                string json = File.ReadAllText(pendingQueuePath);
                return JsonSerializer.Deserialize<List<QueuedRequest>>(json) ?? new List<QueuedRequest>();
            }
            catch
            {
                return new List<QueuedRequest>();
            }
        }

        private void SaveQueue(List<QueuedRequest> queue)
        {
            string json = JsonSerializer.Serialize(queue, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(pendingQueuePath, json);
        }

        private async Task ProcessPendingQueueAsync()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(60));

                List<QueuedRequest> queue;
                lock (queueLock)
                {
                    queue = LoadQueue();
                    if (queue.Count == 0) continue;
                }

                Log($"[Queue] Processing {queue.Count} pending requests...");
                var remaining = new List<QueuedRequest>();

                foreach (var item in queue)
                {
                    try
                    {
                        var payload = JsonSerializer.Deserialize<object>(item.Payload);
                        var result = SendRequest<ApiResponse>(item.Method, item.Endpoint, payload!);
                        
                        if (result == null)
                        {
                            remaining.Add(item);
                        }
                        else
                        {
                            Log($"[Queue] Successfully processed: {item.Method} {item.Endpoint}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[Queue] Failed to process {item.Method} {item.Endpoint}: {ex.Message}");
                        remaining.Add(item);
                    }
                }

                lock (queueLock)
                {
                    SaveQueue(remaining);
                }

                if (remaining.Count < queue.Count)
                {
                    Log($"[Queue] Processed {queue.Count - remaining.Count} requests, {remaining.Count} remaining");
                }
            }
        }

        #endregion

        private void Log(string message)
        {
            Console.WriteLine("[Squidcup] " + message);
        }
    }

    #region DTOs

    public class ApiConfig
    {
        public string? ApiBaseUrl { get; set; }
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
    }

    public class InitMatchRequest
    {
        public long? MatchId { get; set; }
        public string Team1Name { get; set; } = "";
        public string Team2Name { get; set; } = "";
        public string ServerIp { get; set; } = "";
        public string SeriesType { get; set; } = "";
        public int MapNumber { get; set; }
        public string MapName { get; set; } = "";
    }

    public class InitMatchResponse
    {
        public long? MatchId { get; set; }
        public bool Success { get; set; }
    }

    public class PlayerStatsUpdate
    {
        public long SteamId64 { get; set; }
        public string Team { get; set; } = "";
        public string Name { get; set; } = "";
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Damage { get; set; }
        public int Assists { get; set; }
        public int Enemy5ks { get; set; }
        public int Enemy4ks { get; set; }
        public int Enemy3ks { get; set; }
        public int Enemy2ks { get; set; }
        public int UtilityCount { get; set; }
        public int UtilityDamage { get; set; }
        public int UtilitySuccesses { get; set; }
        public int UtilityEnemies { get; set; }
        public int FlashCount { get; set; }
        public int FlashSuccesses { get; set; }
        public int HealthPointsRemovedTotal { get; set; }
        public int HealthPointsDealtTotal { get; set; }
        public int ShotsFiredTotal { get; set; }
        public int ShotsOnTargetTotal { get; set; }
        public int V1Count { get; set; }
        public int V1Wins { get; set; }
        public int V2Count { get; set; }
        public int V2Wins { get; set; }
        public int EntryCount { get; set; }
        public int EntryWins { get; set; }
        public int EquipmentValue { get; set; }
        public int MoneySaved { get; set; }
        public int KillReward { get; set; }
        public int LiveTime { get; set; }
        public int HeadShotKills { get; set; }
        public int CashEarned { get; set; }
        public int EnemiesFlashed { get; set; }
    }

    public class UpdatePlayersRequest
    {
        public List<PlayerStatsUpdate> Players { get; set; } = new();
    }

    public class UpdateMapScoresRequest
    {
        public int Team1Score { get; set; }
        public int Team2Score { get; set; }
    }

    public class EndMapRequest
    {
        public string WinnerName { get; set; } = "";
        public int Team1Score { get; set; }
        public int Team2Score { get; set; }
        public int Team1SeriesScore { get; set; }
        public int Team2SeriesScore { get; set; }
    }

    public class EndMatchRequest
    {
        public string WinnerName { get; set; } = "";
        public int Team1Score { get; set; }
        public int Team2Score { get; set; }
    }

    public class QueuedRequest
    {
        public string Method { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public string Payload { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
