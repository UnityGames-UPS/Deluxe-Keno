using UnityEngine;


internal static class GameLogger
{
    private const string PREFIX_SOCKET = "[Socket]";
    private const string PREFIX_CONNECTION = "[Connection]";

    internal static void Log(string message)
    {
        // Disabled in production - only enable for debugging
        // Debug.Log(message);
    }

    private static void LogConnection(string message)
    {
        Debug.Log($"{PREFIX_CONNECTION} {message}");
    }

    private static void LogConnectionWarning(string message)
    {
        Debug.LogWarning($"{PREFIX_CONNECTION} {message}");
    }

    private static void LogConnectionError(string message)
    {
        Debug.LogError($"{PREFIX_CONNECTION} {message}");
    }

    private static void LogServerSend(string eventName, string json = null)
    {
        if (string.IsNullOrEmpty(json))
            Debug.Log($"{PREFIX_SOCKET} Sending: {eventName}");
        else
            Debug.Log($"{PREFIX_SOCKET} Sending {eventName}: {json}");
    }

    private static void LogServerResponse(string eventName, string json)
    {
        Debug.Log($"{PREFIX_SOCKET} Received {eventName}: {json}");
    }

    internal static void LogError(string message)
    {
        Debug.LogError(message);
    }
}