using System.Runtime.InteropServices;
using UnityEngine;

public class JSBridge : MonoBehaviour
{
    internal static JSBridge Instance;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendLogToReactNative(string message);

    [DllImport("__Internal")]
    private static extern void SendPostMessage(string message);

    [DllImport("__Internal")]
    private static extern void RegisterVisibilityChangeListener(string gameObjectName);
#endif

    private static string _authToken;
    private static string _socketURL;
    private static string _namespace;
    private static bool _hasAuthToken;

    public static string AuthToken => _authToken;
    public static string SocketURL => _socketURL;
    public static string Namespace => _namespace;
    public static bool HasAuthToken => _hasAuthToken;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        RegisterVisibilityListener(gameObject.name);
    }

    private void OnEnable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.logMessageReceived += HandleLog;
#endif
    }

    private void OnDisable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.logMessageReceived -= HandleLog;
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string formattedMessage = $"[{type}] {logString}";
        SendLogToReactNative(formattedMessage);
    }
#endif

    public static void RequestAuthToken()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SendPostMessage("authToken");
#endif
    }

    public static void SendMessage(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        SendPostMessage(message);
#endif
    }

    public void RegisterVisibilityListener(string gameObjectName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log($"[JS] Registering visibility change listener on '{gameObjectName}'");
        RegisterVisibilityChangeListener(gameObjectName);
#else
        Debug.Log("[JS] Visibility listener not registered (editor mode)");
#endif
    }

    public static void NotifyGameEntered()
    {
        SendMessage("OnEnter");
    }

    public static void NotifyGameExit()
    {
        SendMessage("OnExit");
    }

    public static void NotifyError()
    {
        SendMessage("error");
    }

    public static void SetAuthTokenData(string cookie, string socketURL, string nameSpace)
    {
        _authToken = cookie;
        _socketURL = socketURL;
        _namespace = nameSpace;
        _hasAuthToken = true;
    }

    public void OnFocusChanged(string value)
    {
        bool focused = value == "1";
        Debug.Log($"[JSBridge] OnFocusChanged received: {value} (focused: {focused})");
        AudioManager.Instance?.SetMuteAll(!focused);
        if (GameController.Instance != null)
        {
            GameController.Instance.HandleFocusChange(focused);
        }
    }

    public void ReceiveAuthToken(string jsonData)
    {
        Debug.Log($"[JSBridge] ReceiveAuthToken received: {jsonData}");
        try
        {
            AuthTokenData data = JsonUtility.FromJson<AuthTokenData>(jsonData);
            _authToken = data.cookie;
            _socketURL = data.socketURL;
            _namespace = data.nameSpace;
            _hasAuthToken = true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[JSBridge] ReceiveAuthToken error: {ex.Message}");
            _hasAuthToken = false;
        }
    }

    public static void ResetAuthToken()
    {
        _authToken = null;
        _socketURL = null;
        _namespace = null;
        _hasAuthToken = false;
    }
}

[System.Serializable]
public class AuthTokenData
{
    public string cookie;
    public string socketURL;
    public string nameSpace;
}
