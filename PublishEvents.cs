using System.Text;
using System.Text.Json;


namespace Squidcup
{
    public partial class Squidcup
    {
        public async Task SendEventAsync(SquidcupEvent @event)
        {
            try
            {
                if (string.IsNullOrEmpty(matchConfig.RemoteLogURL)) return;

                Log($"[SendEventAsync] Sending Event: {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} on {matchConfig.RemoteLogURL}");

                using var httpClient = new HttpClient();
                using var jsonContent = new StringContent(JsonSerializer.Serialize(@event, @event.GetType()), Encoding.UTF8, "application/json");

                string jsonString = await jsonContent.ReadAsStringAsync();

                Log($"[SendEventAsync] SENDING DATA: {jsonString}");

                if (!string.IsNullOrEmpty(matchConfig.RemoteLogHeaderKey) && !string.IsNullOrEmpty(matchConfig.RemoteLogHeaderValue))
                {
                    httpClient.DefaultRequestHeaders.Add(matchConfig.RemoteLogHeaderKey, matchConfig.RemoteLogHeaderValue);
                }

                var httpResponseMessage = await httpClient.PostAsync(matchConfig.RemoteLogURL, jsonContent);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    Log($"[SendEventAsync] Sending {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} successful with status code: {httpResponseMessage.StatusCode}");
                }
                else
                {
                    Log($"[SendEventAsync] Sending {@event.EventName} for matchId: {liveMatchId} mapNumber: {matchConfig.CurrentMapNumber} failed with status code: {httpResponseMessage.StatusCode}, ResponseContent: {await httpResponseMessage.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception e)
            {
                Log($"[SendEventAsync FATAL] An error occurred: {e.Message}");
            }
        }

        public async Task SendMatchEndNotificationAsync(long matchId)
        {
            try
            {
                Log($"[SendMatchEndNotificationAsync] Starting match end notification process for matchId: {matchId}");
                Log($"[SendMatchEndNotificationAsync] MatchEndRoute value: '{matchConfig.MatchEndRoute}'");
                
                if (string.IsNullOrEmpty(matchConfig.MatchEndRoute))
                {
                    Log($"[SendMatchEndNotificationAsync] MatchEndRoute is null or empty, skipping notification for matchId: {matchId}");
                    return;
                }

                Log($"[SendMatchEndNotificationAsync] Sending match end notification for matchId: {matchId} to {matchConfig.MatchEndRoute}");

                Log($"[SendMatchEndNotificationAsync] Creating HttpClient for matchId: {matchId}");
                using var httpClient = new HttpClient();
                
                Log($"[SendMatchEndNotificationAsync] Creating payload for matchId: {matchId}");
                var payload = new { matchId = matchId.ToString() };
                
                Log($"[SendMatchEndNotificationAsync] Serializing payload for matchId: {matchId}");
                string serializedPayload = JsonSerializer.Serialize(payload);
                Log($"[SendMatchEndNotificationAsync] Serialized payload: {serializedPayload}");
                
                Log($"[SendMatchEndNotificationAsync] Creating StringContent for matchId: {matchId}");
                using var jsonContent = new StringContent(serializedPayload, Encoding.UTF8, "application/json");

                Log($"[SendMatchEndNotificationAsync] Reading content as string for logging purposes for matchId: {matchId}");
                string jsonString = await jsonContent.ReadAsStringAsync();
                Log($"[SendMatchEndNotificationAsync] SENDING DATA: {jsonString}");

                Log($"[SendMatchEndNotificationAsync] Checking for remote log headers for matchId: {matchId}");
                Log($"[SendMatchEndNotificationAsync] RemoteLogHeaderKey: '{matchConfig.RemoteLogHeaderKey}', RemoteLogHeaderValue: '{matchConfig.RemoteLogHeaderValue}'");
                
                if (!string.IsNullOrEmpty(matchConfig.RemoteLogHeaderKey) && !string.IsNullOrEmpty(matchConfig.RemoteLogHeaderValue))
                {
                    Log($"[SendMatchEndNotificationAsync] Adding header '{matchConfig.RemoteLogHeaderKey}' for matchId: {matchId}");
                    httpClient.DefaultRequestHeaders.Add(matchConfig.RemoteLogHeaderKey, matchConfig.RemoteLogHeaderValue);
                    Log($"[SendMatchEndNotificationAsync] Header added successfully for matchId: {matchId}");
                }
                else
                {
                    Log($"[SendMatchEndNotificationAsync] No headers to add for matchId: {matchId}");
                }

                Log($"[SendMatchEndNotificationAsync] Making POST request to {matchConfig.MatchEndRoute} for matchId: {matchId}");
                var httpResponseMessage = await httpClient.PostAsync(matchConfig.MatchEndRoute, jsonContent);
                Log($"[SendMatchEndNotificationAsync] POST request completed for matchId: {matchId}");

                Log($"[SendMatchEndNotificationAsync] Response status code: {httpResponseMessage.StatusCode} for matchId: {matchId}");
                Log($"[SendMatchEndNotificationAsync] IsSuccessStatusCode: {httpResponseMessage.IsSuccessStatusCode} for matchId: {matchId}");

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    Log($"[SendMatchEndNotificationAsync] Match end notification for matchId: {matchId} sent successfully with status code: {httpResponseMessage.StatusCode}");
                    
                    try
                    {
                        string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
                        Log($"[SendMatchEndNotificationAsync] Response content for matchId: {matchId}: {responseContent}");
                    }
                    catch (Exception ex)
                    {
                        Log($"[SendMatchEndNotificationAsync] Could not read response content for matchId: {matchId}: {ex.Message}");
                    }
                }
                else
                {
                    try
                    {
                        string responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
                        Log($"[SendMatchEndNotificationAsync] Failed to send match end notification for matchId: {matchId}. Status code: {httpResponseMessage.StatusCode}, ResponseContent: {responseContent}");
                    }
                    catch (Exception ex)
                    {
                        Log($"[SendMatchEndNotificationAsync] Failed to send match end notification for matchId: {matchId}. Status code: {httpResponseMessage.StatusCode}, Could not read response content: {ex.Message}");
                    }
                }
                
                Log($"[SendMatchEndNotificationAsync] Match end notification process completed for matchId: {matchId}");
            }
            catch (Exception e)
            {
                Log($"[SendMatchEndNotificationAsync FATAL] An error occurred for matchId: {matchId}: {e.Message}");
                Log($"[SendMatchEndNotificationAsync FATAL] Stack trace for matchId: {matchId}: {e.StackTrace}");
                Log($"[SendMatchEndNotificationAsync FATAL] Full exception details for matchId: {matchId}: {e}");
            }
        }
    }
}
