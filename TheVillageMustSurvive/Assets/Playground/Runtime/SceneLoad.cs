using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Playground.Runtime
{
    public class SceneLoad : NetworkBehaviour
    {
#if UNITY_EDITOR
        [Tooltip("Drag your Scene asset here to auto-populate the Scene Name at edit time.")]
        public SceneAsset SceneAsset;

        private void OnValidate()
        {
            if (SceneAsset != null)
            {
                m_SceneName = SceneAsset.name;
            }
        }
#endif

        [Header("Runtime Scene Name (do not include file extension)")]
        [SerializeField]
        private string m_SceneName;

        // Remember what we were in before loading the new scene
        private Scene m_PreviousSceneName;
        // Cache of the newly loaded scene
        private Scene m_LoadedScene;

        /// <summary>
        /// True once the new scene has finished loading.
        /// </summary>
        public bool SceneIsLoaded =>
            m_LoadedScene.IsValid() && m_LoadedScene.isLoaded;

        /// <summary>
        /// Example server-side validation: rejects any Single load to force additive.
        /// Extend this if you need to block certain scenes.
        /// </summary>
        private bool ServerSideSceneValidation(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            // Force additive only
            return loadSceneMode == LoadSceneMode.Additive;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && !string.IsNullOrEmpty(m_SceneName))
            {
                m_PreviousSceneName = SceneManager.GetSceneByName("Lobby");

                // Hook our validation callback and scene-event listener
                NetworkManager.SceneManager.VerifySceneBeforeLoading = ServerSideSceneValidation;
                NetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;

                // Kick off the additive load
                var status = NetworkManager.SceneManager.LoadScene(
                    m_SceneName,
                    LoadSceneMode.Additive
                );
                CheckStatus(status, isLoading: true);
            }
            base.OnNetworkSpawn();
        }

        private void CheckStatus(SceneEventProgressStatus status, bool isLoading)
        {
            var action = isLoading ? "load" : "unload";
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"[SceneLoad] Failed to {action} '{m_SceneName}': {status}");
            }
        }

        private void SceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            // Once the SERVER has successfully finished loading our target scene...
            if (sceneEvent.SceneEventType == SceneEventType.LoadComplete &&
                sceneEvent.ClientId == NetworkManager.ServerClientId &&
                sceneEvent.SceneName == m_SceneName)
            {
                m_LoadedScene = sceneEvent.Scene;
                Debug.Log($"[SceneLoad] Loaded '{sceneEvent.SceneName}'. Unloading previous '{m_PreviousSceneName}'.");
                
                var unloadStatus = NetworkManager.SceneManager.UnloadScene(m_PreviousSceneName);
                CheckStatus(unloadStatus, isLoading: false);
            }
        }

        /// <summary>
        /// (Optional) Manual unload of the currently loaded scene, if you ever need it.
        /// </summary>
        public void UnloadLoadedScene()
        {
            if (!IsServer || !IsSpawned || !m_LoadedScene.IsValid() || !m_LoadedScene.isLoaded)
                return;

            var status = NetworkManager.SceneManager.UnloadScene(m_LoadedScene);
            CheckStatus(status, isLoading: false);
        }
    }
}
