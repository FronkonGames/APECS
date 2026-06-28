////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) Martin Bustos @FronkonGames <fronkongames@gmail.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
// documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of
// the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FronkonGames.APECS
{
  /// <summary>
  /// MonoBehaviour that drives the APECS scheduler from the Unity game loop and URP. PreRender ticks are wired
  /// to <see cref="RenderPipelineManager.beginContextRendering"/> so they fire once per frame, before URP starts
  /// submitting any camera.
  /// </summary>
  [DefaultExecutionOrder(-1000)]
  [DisallowMultipleComponent]
  public sealed class WorldBootstrap : MonoBehaviour
  {
    /// <summary> ECS world owned by this bootstrap. </summary>
    public World World
    {
      /// <summary> Gets the bootstrapped world instance. </summary>
      get;
      private set;
    }

    /// <summary> System scheduler driven by Unity's update loop. </summary>
    public SystemScheduler Scheduler
    {
      /// <summary> Gets the bootstrapped scheduler instance. </summary>
      get;
      private set;
    }

    [SerializeField]
    private string worldName = "Default";
    
    [SerializeField]
    private bool autoDiscoverSystems = true;
    
    [SerializeField]
    private bool registerWorldTime = true;

    private WorldTime time;
    private readonly bool suspended;

    private void Awake()
    {
      World = new World(worldName);
      World.Default = World;
      if (registerWorldTime == true)
      {
        time = new WorldTime();
        World.Resources.AddValue(time);
      }

      Scheduler = new SystemScheduler();
      Scheduler.Initialize(World, autoDiscoverSystems);
    }

    private void OnEnable() => RenderPipelineManager.beginContextRendering += OnBeginContextRendering;

    private void OnDisable() => RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;

    private void Update()
    {
      if (suspended == true || Scheduler == null)
        return;

      RefreshTime();

      Scheduler.Tick(Phase.PreUpdate);
      Scheduler.Tick(Phase.Update);
      Scheduler.Tick(Phase.PostUpdate);
    }

    private void FixedUpdate()
    {
      if (suspended == true || Scheduler == null)
        return;

      RefreshTime();

      Scheduler.Tick(Phase.FixedUpdate);
    }

    private void LateUpdate()
    {
      if (suspended == true || Scheduler == null)
        return;

      Scheduler.Tick(Phase.LateUpdate);
    }

    private void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
    {
      // Fires once per frame, before URP renders any camera. This is the URP-native
      // equivalent of a "before Camera.Render" callback.
      if (suspended == true || Scheduler == null)
        return;

      Scheduler.Tick(Phase.PreRender);
    }

    private void RefreshTime()
    {
      if (registerWorldTime == false || World == null)
        return;

      ref var t = ref World.Resources.GetValue<WorldTime>();
      t.deltaTime = Time.deltaTime;
      t.fixedDeltaTime = Time.fixedDeltaTime;
      t.unscaledDeltaTime = Time.unscaledDeltaTime;
      t.elapsedTime = Time.timeAsDouble;
      t.frameCount = Time.frameCount;
    }

    private void OnDestroy()
    {
      Scheduler?.Shutdown();
      Scheduler?.Dispose();
      Scheduler = null;
      World?.Dispose();
      World = null;

      if (ReferenceEquals(World.Default, World) == true)
        World.Default = null;
    }
  }
}
