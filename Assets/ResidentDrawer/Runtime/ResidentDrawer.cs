using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ResidentDrawer
{
    /// <summary>
    /// Utility struct to pass GPU resident drawer settings together
    /// </summary>
    public struct ResidentDrawerSettings
    {
        /// <summary>
        /// Does the implementor support dithered crossfade
        /// </summary>
        public bool supportDitheringCrossFade;

        /// <summary>
        /// Enable GPU data for occlusion culling
        /// </summary>
        public bool enableOcclusionCulling;

        /// <summary>
        /// Allows the GPU Resident Drawer to run in edit mode
        /// </summary>
        public bool allowInEditMode;

        /// <summary>
        /// Default minimum screen percentage (0-20%) gpu-driven Renderers can cover before getting culled.
        /// </summary>
        public float smallMeshScreenPercentage;

#if UNITY_EDITOR
        /// <summary>
        /// Shader used if no custom picking pass has been implemented
        /// </summary>
        public Shader pickingShader;
#endif

        /// <summary>
        /// Shader used when an error is detected
        /// </summary>
        public Shader errorShader;

        /// <summary>
        /// Shader used while compiling shaders
        /// </summary>
        public Shader loadingShader;
    }
    
    public class ResidentDrawer
    {
        internal static ResidentDrawer instance { get => s_Instance; }
        private static ResidentDrawer s_Instance = null;
        
        private void InsertIntoPlayerLoop()
        {
            var rootLoop = PlayerLoop.GetCurrentPlayerLoop();
            bool isAdded = false;

            for (var i = 0; i < rootLoop.subSystemList.Length; i++)
            {
                var subSystem = rootLoop.subSystemList[i];

                // We have to update inside the PostLateUpdate systems, because we have to be able to get previous matrices from renderers.
                // Previous matrices are updated by renderer managers on UpdateAllRenderers which is part of PostLateUpdate.
                if (!isAdded && subSystem.type == typeof(PostLateUpdate))
                {
                    var subSubSystems = new List<PlayerLoopSystem>();
                    foreach (var subSubSystem in subSystem.subSystemList)
                    {
                        if (subSubSystem.type == typeof(PostLateUpdate.FinishFrameRendering))
                        {
                            PlayerLoopSystem s = default;
                            s.updateDelegate += PostPostLateUpdateStatic;
                            s.type = GetType();
                            subSubSystems.Add(s);
                            isAdded = true;
                        }

                        subSubSystems.Add(subSubSystem);
                    }

                    subSystem.subSystemList = subSubSystems.ToArray();
                    rootLoop.subSystemList[i] = subSystem;
                }
            }

            PlayerLoop.SetPlayerLoop(rootLoop);
        }
        
        private void RemoveFromPlayerLoop()
        {
            var rootLoop = PlayerLoop.GetCurrentPlayerLoop();

            for (int i = 0; i < rootLoop.subSystemList.Length; i++)
            {
                var subsystem = rootLoop.subSystemList[i];
                if (subsystem.type != typeof(PostLateUpdate))
                    continue;

                var newList = new List<PlayerLoopSystem>();
                foreach (var subSubSystem in subsystem.subSystemList)
                {
                    if (subSubSystem.type != GetType())
                        newList.Add(subSubSystem);
                }
                subsystem.subSystemList = newList.ToArray();
                rootLoop.subSystemList[i] = subsystem;
            }
            PlayerLoop.SetPlayerLoop(rootLoop);
        }

#if UNITY_EDITOR
        private NativeList<int> m_FrameCameraIDs;
        private bool m_FrameUpdateNeeded = false;

        private bool m_SelectionChanged;
        
        private static void OnAssemblyReload()
        {
            if (s_Instance is not null)
                s_Instance.Dispose();
        }
#endif

        internal static bool IsEnabled()
        {
            return s_Instance is not null;
        }
        
        /// <summary>
        /// Enable or disable GPUResidentDrawer based on the project settings.
        /// We call this every frame because GPUResidentDrawer can be enabled/disabled by the settings outside the render pipeline asset.
        /// </summary>
        public static void ReinitializeIfNeeded()
        {
            Reinitialize();
        }
        
        internal static void Reinitialize()
        {
            ResidentDrawerSettings settings = default;

            // When compiling in the editor, we include a try catch block around our initialization logic to avoid leaving the editor window in a broken state if something goes wrong.
            // We can probably remove this in the future once the edit mode functionality stabilizes, but for now it's safest to have a fallback.
#if UNITY_EDITOR
            try
#endif
            {
                Recreate(settings);
            }
#if UNITY_EDITOR
            catch (Exception exception)
            {
                Debug.LogError($"The GPU Resident Drawer encountered an error during initialization. The standard SRP path will be used instead. [Error: {exception.Message}]");
                Debug.LogError($"GPU Resident drawer stack trace: {exception.StackTrace}");
                CleanUp();
            }
#endif
        }

        private static void CleanUp()
        {
            if (s_Instance == null)
                return;

            s_Instance.Dispose();
            s_Instance = null;
        }

        private static void Recreate(ResidentDrawerSettings settings)
        {
            CleanUp();
            s_Instance = new ResidentDrawer(settings, 4096);
        }
        
        ScriptableRenderContext m_Context = default;

        private SceneDataProcessor m_SceneDataProcessor = null;

        private RenderersBatchersContext m_BatchersContext = null;
        internal ResidentDrawerSettings settings { get => m_Settings; }

        private ResidentDrawerSettings m_Settings;
        internal InstanceCullingBatcher instanceCullingBatcher { get => m_Batcher; }

        private InstanceCullingBatcher m_Batcher = null;
        
        private ResidentDrawer(ResidentDrawerSettings settings, int maxInstanceCount)
        {
            var renderPipelineAsset = GraphicsSettings.currentRenderPipeline;
            m_Settings = settings;

            m_SceneDataProcessor = new SceneDataProcessor();
            
            var rbcDesc = RenderersBatchersContextDesc.NewDefault();
            rbcDesc.instanceNum = maxInstanceCount;
            rbcDesc.supportDitheringCrossFade = settings.supportDitheringCrossFade;
            rbcDesc.smallMeshScreenPercentage = settings.smallMeshScreenPercentage;
            rbcDesc.enableBoundingSpheresInstanceData = settings.enableOcclusionCulling;
            rbcDesc.enableCullerDebugStats = true; // for now, always allow the possibility of reading counter stats from the cullers.

            var instanceCullingBatcherDesc = InstanceCullingBatcherDesc.NewDefault();
#if UNITY_EDITOR
            instanceCullingBatcherDesc.brgPicking = settings.pickingShader;
            instanceCullingBatcherDesc.brgLoading = settings.loadingShader;
            instanceCullingBatcherDesc.brgError = settings.errorShader;
#endif

            m_BatchersContext = new RenderersBatchersContext(rbcDesc);
            m_Batcher = new InstanceCullingBatcher(m_BatchersContext, instanceCullingBatcherDesc);

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload += OnAssemblyReload;
            m_FrameCameraIDs = new NativeList<int>(1, Allocator.Persistent);
#endif
            SceneManager.sceneLoaded += OnSceneLoaded;

            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
            RenderPipelineManager.endContextRendering += OnEndContextRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#if UNITY_EDITOR
            Selection.selectionChanged += OnSelectionChanged;
#endif

            // GPU Resident Drawer only supports legacy lightmap binding.
            // Accordingly, we set the keyword globally across all shaders.
            const string useLegacyLightmapsKeyword = "USE_LEGACY_LIGHTMAPS";
            Shader.EnableKeyword(useLegacyLightmapsKeyword);

            InsertIntoPlayerLoop();
        }
        
        private void Dispose()
        {
            Assert.IsNotNull(s_Instance);

#if UNITY_EDITOR
            AssemblyReloadEvents.beforeAssemblyReload -= OnAssemblyReload;
            if (m_FrameCameraIDs.IsCreated)
                m_FrameCameraIDs.Dispose();
#endif
            SceneManager.sceneLoaded -= OnSceneLoaded;

            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.endContextRendering -= OnEndContextRendering;
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
#if UNITY_EDITOR
            Selection.selectionChanged -= OnSelectionChanged;
#endif

            RemoveFromPlayerLoop();

            const string useLegacyLightmapsKeyword = "USE_LEGACY_LIGHTMAPS";
            Shader.DisableKeyword(useLegacyLightmapsKeyword);

            s_Instance = null;
            m_Batcher?.Dispose();
            m_BatchersContext.Dispose();
            m_SceneDataProcessor.Dispose();
            
            m_Context = default;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Loaded scene might contain light probes that would affect existing objects. Hence we have to update all probes data.
            // if(mode == LoadSceneMode.Additive)
            //     m_BatchersContext.UpdateAmbientProbeAndGpuBuffer(forceUpdate: true);
        }
        
        private static void PostPostLateUpdateStatic()
        {
            s_Instance?.PostPostLateUpdate();
        }

        private void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (s_Instance is null)
                return;

            // This logic ensures that EditorFrameUpdate is not called more than once after calling BeginContextRendering, unless EndContextRendering has also been called.
            if (m_Context == default)
            {
                m_Context = context;
#if UNITY_EDITOR
                EditorFrameUpdate(cameras);
#endif
                // m_Batcher.OnBeginContextRendering();
            }
        }
        
        #if UNITY_EDITOR
        // If running in the editor the player loop might not run
        // In order to still have a single frame update we keep track of the camera ids
        // A frame update happens in case the first camera is rendered again
        private void EditorFrameUpdate(List<Camera> cameras)
        {
            bool newFrame = false;
            foreach (Camera camera in cameras)
            {
                int instanceID = camera.GetInstanceID();
                if (m_FrameCameraIDs.Length == 0 || m_FrameCameraIDs.Contains(instanceID))
                {
                    newFrame = true;
                    m_FrameCameraIDs.Clear();
                }
                m_FrameCameraIDs.Add(instanceID);
            }

            if (newFrame)
            {
                if (m_FrameUpdateNeeded)
                    m_Batcher.UpdateFrame();
                else
                    m_FrameUpdateNeeded = true;
            }

            ProcessSelection();
        }

        private void OnSelectionChanged()
        {
            m_SelectionChanged = true;
        }

        private void ProcessSelection()
        {
            if(!m_SelectionChanged)
                return;

            m_SelectionChanged = false;

            Object[] renderers = Selection.GetFiltered(typeof(MeshRenderer), SelectionMode.Deep);

            var rendererIDs = new NativeArray<int>(renderers.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < renderers.Length; ++i)
                rendererIDs[i] = renderers[i] ? renderers[i].GetInstanceID() : 0;

            m_Batcher.UpdateSelectedRenderers(rendererIDs);

            rendererIDs.Dispose();
        }
#endif

        private void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (s_Instance is null)
                return;

            if (m_Context == context)
            {
                m_Context = default;
                m_Batcher.OnEndContextRendering();
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            m_Batcher.OnBeginCameraRendering(camera);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            m_Batcher.OnEndCameraRendering(camera);
        }

        // Do not trace anything like Unity 6 do, focus on static things.
        // Resident drawer only add renderer and lodGroup, material and meshes are added automatically.
        private void PostPostLateUpdate()
        {
            Profiler.BeginSample("GPUResidentDrawer.ProcessLODGroups");
            ProcessLODGroups();
            Profiler.EndSample();

            Profiler.BeginSample("GPUResidentDrawer.ProcessRenderers");
            ProcessRenderers(rendererData, unsupportedRenderers.AsArray());
            Profiler.EndSample();

            Profiler.BeginSample("GPUResidentDrawer.ProcessRendererMaterialChanges");
            ProcessRendererMaterialChanges(rendererData.changedID, supportedChangedMaterials.AsArray(), supportedChangedPackedMaterialDatas.AsArray());
            Profiler.EndSample();

            // m_BatchersContext.UpdateInstanceMotions();

            m_Batcher.UpdateFrame();

            m_SceneDataProcessor.Clear();
#if UNITY_EDITOR
            m_FrameUpdateNeeded = false;
#endif
        }
        
        private void ProcessLODGroups()
        {
            m_BatchersContext.UpdateLODGroupData(m_SceneDataProcessor.lodGroupInputData.AsReadOnly());
        }
        
        private void ProcessRenderers()
        {
            RendererGroupInputData.ReadOnly rendererData = m_SceneDataProcessor.rendererInputData.AsReadOnly();
            var changedInstances = new NativeArray<InstanceHandle>(rendererData.rendererGroupID.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            ScheduleQueryRendererGroupInstancesJob(rendererData, changedInstances).Complete();

            m_Batcher.DestroyDrawInstances(changedInstances);
            changedInstances.Dispose();
            m_Batcher.UpdateRenderers(rendererData);

            Profiler.EndSample();

            Profiler.BeginSample("GPUResidentDrawer.TransformMeshRenderers");
            var transformChanges = m_Dispatcher.GetTransformChangesAndClear<MeshRenderer>(TransformTrackingType.GlobalTRS, Allocator.TempJob);
            var transformedInstances = new NativeArray<InstanceHandle>(transformChanges.transformedID.Length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            ScheduleQueryRendererGroupInstancesJob(transformChanges.transformedID, transformedInstances).Complete();
            // We can pull localToWorldMatrices directly from the renderers if we are doing update after PostLatUpdate.
            // This will save us transform re computation as matrices are ready inside renderer's TransformInfo.
            TransformInstances(transformedInstances, transformChanges.localToWorldMatrices);
            transformedInstances.Dispose();
            transformChanges.Dispose();
        }

        private void FreeRendererGroupInstances(NativeArray<int>.ReadOnly rendererGroupIDs)
        {
            Profiler.BeginSample("GPUResidentDrawer.FreeRendererGroupInstances");

            m_Batcher.FreeRendererGroupInstances(rendererGroupIDs);

            Profiler.EndSample();
        }

        private JobHandle ScheduleQueryRendererGroupInstancesJob(RendererGroupInputData.ReadOnly inputData, NativeArray<InstanceHandle> instances)
        {
            return m_BatchersContext.ScheduleQueryRendererGroupInstancesJob(inputData.rendererGroupID, instances);
        }
    }
}
