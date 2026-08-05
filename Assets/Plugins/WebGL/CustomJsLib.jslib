mergeInto(LibraryManager.library, {
  SendLogToReactNative: function (messagePtr) {
    try {
      var message = UTF8ToString(messagePtr);
      if (typeof window !== "undefined" && window.ReactNativeWebView) {
        if (typeof window.ReactNativeWebView.postMessage !== "undefined" && window.ReactNativeWebView.postMessage) {
          window.ReactNativeWebView.postMessage(message);
        }
      }
    } catch (e) {
      console.error("[CustomJsLib] SendLogToReactNative Error:", e);
    }
  },

  SendPostMessage: function (messagePtr) {
    try {
      var message = UTF8ToString(messagePtr);
      console.log('sending msg: ', message);
      if (typeof window !== "undefined" && window.ReactNativeWebView) {
        if (typeof window.ReactNativeWebView.postMessage !== "undefined" && window.ReactNativeWebView.postMessage) {
          if(message == "authToken"){
            window.ReactNativeWebView.postMessage("if message is authtoken");
            var injectedObjectJson = window.ReactNativeWebView.injectedObjectJson();
            var injectedObj = JSON.parse(injectedObjectJson);

            window.ReactNativeWebView.postMessage('Injected obj : ' + injectedObjectJson);
            
            var combinedData = JSON.stringify({
                socketURL: injectedObj.socketURL.trim(),
                cookie: injectedObj.token.trim(),
                nameSpace: injectedObj.nameSpace ? injectedObj.nameSpace.trim() : ""
            });

            if (typeof SendMessage === 'function') {
              SendMessage('SocketManager', 'ReceiveAuthToken', combinedData);
              SendMessage('JSBridge', 'ReceiveAuthToken', combinedData);
            } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
              unityInstance.SendMessage('SocketManager', 'ReceiveAuthToken', combinedData);
              unityInstance.SendMessage('JSBridge', 'ReceiveAuthToken', combinedData);
            }
          }
          window.ReactNativeWebView.postMessage(message);
        }
      } 
      else if (typeof window !== "undefined" && window.parent) {
        if (typeof window.parent.dispatchReactUnityEvent !== "undefined" && window.parent.dispatchReactUnityEvent) {
          window.parent.dispatchReactUnityEvent(message);
        }
      }
    } catch (e) {
      console.error("[CustomJsLib] SendPostMessage Error:", e);
    }
  },

  RegisterVisibilityChangeListener: function(gameObjectNamePtr) {
    var gameObjectName = UTF8ToString(gameObjectNamePtr);
    console.log('[JS] RegisterVisibilityChangeListener called for GameObject:', gameObjectName);

    function setUnityAudioSuspended(suspended) {
        try {
            var wa = (typeof WEBAudio !== 'undefined') ? WEBAudio
                   : (typeof Module !== 'undefined' && Module.WEBAudio) ? Module.WEBAudio
                   : null;
            if (!wa || !wa.audioContext) return;
            if (suspended) {
                if (wa.audioContext.state === 'running') wa.audioContext.suspend();
            } else {
                if (wa.audioContext.state === 'suspended') wa.audioContext.resume();
            }
        } catch (err) { console.warn('[JS] Unity audio suspend/resume failed:', err); }
    }

    function sendFocusToUnity(focused) {
        console.log('[JS] sendFocusToUnity - focused:', focused);
        setUnityAudioSuspended(!focused);
        try {
            var value = focused ? '1' : '0';
            if (typeof SendMessage === 'function') {
                SendMessage(gameObjectName, 'OnFocusChanged', value);
            } else if (typeof unityInstance !== 'undefined' && unityInstance && unityInstance.SendMessage) {
                unityInstance.SendMessage(gameObjectName, 'OnFocusChanged', value);
            }
        } catch (err) {
            console.error('[JS] Error sending focus message to Unity:', err);
        }
    }

    window._unityVisibilityCallback = function() {
        var hidden = document.hidden || document.webkitHidden;
        console.log('[JS] App visibilitychange - hidden:', hidden);
        sendFocusToUnity(!hidden);
    };
    window._unityWindowBlurCallback  = function() {
        console.log('[JS] App unfocus (window blur)');
        sendFocusToUnity(false);
    };
    window._unityWindowFocusCallback = function() {
        console.log('[JS] App focus (window focus)');
        sendFocusToUnity(true);
    };

    document.removeEventListener('visibilitychange',       window._unityVisibilityCallback);
    document.removeEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
    window.removeEventListener('blur',  window._unityWindowBlurCallback);
    window.removeEventListener('focus', window._unityWindowFocusCallback);

    document.addEventListener('visibilitychange',       window._unityVisibilityCallback);
    document.addEventListener('webkitvisibilitychange', window._unityVisibilityCallback);
    window.addEventListener('blur',  window._unityWindowBlurCallback);
    window.addEventListener('focus', window._unityWindowFocusCallback);
  }
});
