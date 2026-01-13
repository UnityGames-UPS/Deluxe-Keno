using UnityEngine;
using System.Diagnostics;

/// <summary>
/// Optimized logger that strips logs in production builds
/// Only shows necessary connection and response logs
/// </summary>
public static class GameLogger
{
    // Connection logs (always shown)
    public static void LogConnection(string message)
    {
        UnityEngine.Debug.Log($"[CONNECTION] {message}");
    }

    public static void LogConnectionError(string message)
    {
        UnityEngine.Debug.LogError($"[CONNECTION ERROR] {message}");
    }

    public static void LogConnectionWarning(string message)
    {
        UnityEngine.Debug.LogWarning($"[CONNECTION WARNING] {message}");
    }

    // Server response logs (always shown)
    public static void LogServerResponse(string eventName, string data)
    {
        UnityEngine.Debug.Log($"[SERVER] Event: {eventName}\nData: {data}");
    }

    public static void LogServerSend(string eventName, string data = null)
    {
        string message = string.IsNullOrEmpty(data)
            ? $"[SERVER SEND] Event: {eventName}"
            : $"[SERVER SEND] Event: {eventName}\nPayload: {data}";
        UnityEngine.Debug.Log(message);
    }

    // Debug logs (stripped in production)
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(string message)
    {
        UnityEngine.Debug.LogError(message);
    }

    // Game state logs (stripped in production)
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogGameState(string state, string details = "")
    {
        string message = string.IsNullOrEmpty(details)
            ? $"[GAME STATE] {state}"
            : $"[GAME STATE] {state} - {details}";
        UnityEngine.Debug.Log(message);
    }

    // Animation logs (stripped in production)
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogAnimation(string animationName, string details = "")
    {
        string message = string.IsNullOrEmpty(details)
            ? $"[ANIMATION] {animationName}"
            : $"[ANIMATION] {animationName} - {details}";
        UnityEngine.Debug.Log(message);
    }
}