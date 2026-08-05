using System.Runtime.InteropServices;
using UnityEngine;

public class JSBridge : MonoBehaviour
{
    private static JSBridge _instance;

    public static JSBridge Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("JSBridge");
                _instance = go.AddComponent<JSBridge>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

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
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
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

    public void ReceiveAuthToken(string jsonData)
    {
        try
        {
            AuthTokenData data = JsonUtility.FromJson<AuthTokenData>(jsonData);
            _authToken = data.cookie;
            _socketURL = data.socketURL;
            _namespace = data.nameSpace;
            _hasAuthToken = true;
        }
        catch (System.Exception)
        {
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
