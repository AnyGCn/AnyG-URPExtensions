using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering.Universal.Extensions.CachedShadowMap
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [ExecuteInEditMode]
    public class CachedShadowManager : MonoBehaviour
    {
        // Singleton Functions
        static CachedShadowManager s_Instance = null;

        public static CachedShadowManager instance
        {
            get
            {
                if (!s_Instance)
                {
                    GameObject go = new GameObject("Default " + typeof(CachedShadowManager).Name) { hideFlags = HideFlags.HideAndDontSave };

#if !UNITY_EDITOR
                    GameObject.DontDestroyOnLoad(go);
#endif

                    go.AddComponent<Camera>();
                    s_Instance = go.AddComponent<CachedShadowManager>();
                }

                return s_Instance;
            }
        }
        
        public void RegisterCamera(Camera cachedCamera)
        {
            if (!shadowCaches.ContainsKey(cachedCamera))
            {
                ShadowCacheData shadowCacheData = ShadowCacheData.Create(cachedCamera);
                if (shadowCacheData != null)
                {
                    shadowCaches.Add(cachedCamera, shadowCacheData);
                }
            }
        }
        
        public void UnregisterCamera(Camera cachedCamera)
        {
            if (shadowCaches.TryGetValue(cachedCamera, out var cachedShadowMap))
            {
                cachedShadowMap.Dispose();
                shadowCaches.Remove(cachedCamera);
            }
        }
        
        private void Awake()
        {
            if (s_Instance == null && s_Instance != this)
            {
                DestroyImmediate(this);
                return;
            }
            
            shadowCastCamera = GetComponent<Camera>();
            shadowCastCamera.enabled = false;
        }

        private void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;

            foreach (var shadowCache in shadowCaches)
            {
                shadowCache.Value.Dispose();
            }
        }
        
        private Camera shadowCastCamera;
        private Dictionary<Camera, ShadowCacheData> shadowCaches = new Dictionary<Camera, ShadowCacheData>();
        
        private void LateUpdate()
        {
            foreach (var shadowCache in shadowCaches)
            {
                
            }
        }
        
        private class ShadowCacheData : IDisposable
        {
            public readonly Camera _camera;
            public readonly UniversalAdditionalCameraData _additionalCameraData;
            public RenderTexture texture;
            public Vector3 Position;

            public static ShadowCacheData Create(Camera camera)
            {
                if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var additionalCameraData))
                {
                    return new ShadowCacheData(camera, additionalCameraData);
                }

                return null;
            }

            private ShadowCacheData(Camera camera, UniversalAdditionalCameraData additionalCameraData)
            {
                _camera = camera;
                _additionalCameraData = additionalCameraData;
            }
            
            public void Update()
            {
                
            }
            
            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (texture != null)
                    {
                        CoreUtils.Destroy(texture);
                        texture = null;
                    }
                }
            }
            
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }
    }
}