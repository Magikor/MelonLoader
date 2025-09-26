using System;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

#if SM_Il2Cpp
using UnityEngine.Events;
#else
using HarmonyLib;
using System.Reflection;
#endif

#pragma warning disable CA2013

namespace MelonLoader.Support
{
    internal static class SceneHandler
    {
        internal class SceneInitEvent
        {
            internal int buildIndex;
            internal string name;
            internal bool wasLoadedThisTick;
        }

        private static Queue<SceneInitEvent> scenesLoaded = new Queue<SceneInitEvent>();

#if SM_Il2Cpp
        internal static void Init()
#else
        internal static void Init(MethodInfo sceneLoaded, MethodInfo sceneUnloaded)
#endif
        {
#if !SM_Il2Cpp
            if (sceneLoaded != null)
#endif
                try
                {
#if SM_Il2Cpp
                    SceneManager.sceneLoaded = (
                        (ReferenceEquals(SceneManager.sceneLoaded, null))
                        ? new Action<Scene, LoadSceneMode>(OnSceneLoad)
                        : Il2CppSystem.Delegate.Combine(SceneManager.sceneLoaded, (UnityAction<Scene, LoadSceneMode>)new Action<Scene, LoadSceneMode>(OnSceneLoad)).Cast<UnityAction<Scene, LoadSceneMode>>()
                        );
#else
                    MethodInfo onSceneLoadPrefix = typeof(SceneHandler).GetMethod("OnSceneLoadPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                    Core.HarmonyInstance.Patch(sceneLoaded, new HarmonyMethod(onSceneLoadPrefix));
                    MelonLogger.Msg($"Hooked into {sceneLoaded.FullDescription()}");
#endif
                }
                catch (Exception ex) { MelonLogger.Error($"SceneManager.sceneLoaded override failed: {ex}"); }

#if !SM_Il2Cpp
            if (sceneUnloaded != null)
#endif
                try
                {
#if SM_Il2Cpp
                    SceneManager.sceneUnloaded = (
                        (ReferenceEquals(SceneManager.sceneUnloaded, null))
                        ? new Action<Scene>(OnSceneUnload)
                        : Il2CppSystem.Delegate.Combine(SceneManager.sceneUnloaded, (UnityAction<Scene>)new Action<Scene>(OnSceneUnload)).Cast<UnityAction<Scene>>()
                        );
#else
                    MethodInfo onSceneUnloadPrefix = typeof(SceneHandler).GetMethod("OnSceneUnloadPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                    Core.HarmonyInstance.Patch(sceneUnloaded, new HarmonyMethod(onSceneUnloadPrefix));
                    MelonLogger.Msg($"Hooked into {sceneUnloaded.FullDescription()}");
#endif
                }
                catch (Exception ex) { MelonLogger.Error($"SceneManager.sceneUnloaded override failed: {ex}"); }
        }

#if !SM_Il2Cpp
        private static void OnSceneLoadPrefix(Scene __0, LoadSceneMode __1)
            => OnSceneLoad(__0, __1);
        private static void OnSceneUnloadPrefix(Scene __0)
            => OnSceneUnload(__0);
#endif

        private static void OnSceneLoad(Scene scene, LoadSceneMode mode)
        {
            if (Main.obj == null)
                SM_Component.Create();

            if (ReferenceEquals(scene, null))
                return;

            Main.Interface.OnSceneWasLoaded(scene.buildIndex, scene.name);
            scenesLoaded.Enqueue(new SceneInitEvent { buildIndex = scene.buildIndex, name = scene.name });
        }

        private static void OnSceneUnload(Scene scene)
        {
            if (ReferenceEquals(scene, null))
                return;

            Main.Interface.OnSceneWasUnloaded(scene.buildIndex, scene.name);
        }

        internal static void OnUpdate()
        {
            if (scenesLoaded.Count > 0)
            {
                Queue<SceneInitEvent> requeue = new Queue<SceneInitEvent>();
                SceneInitEvent evt = null;
                while ((scenesLoaded.Count > 0) && ((evt = scenesLoaded.Dequeue()) != null))
                {
                    if (evt.wasLoadedThisTick)
                        Main.Interface.OnSceneWasInitialized(evt.buildIndex, evt.name);
                    else
                    {
                        evt.wasLoadedThisTick = true;
                        requeue.Enqueue(evt);
                    }
                }
                while ((requeue.Count > 0) && ((evt = requeue.Dequeue()) != null))
                    scenesLoaded.Enqueue(evt);
            }
        }
    }
}
