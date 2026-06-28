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
using System;
using System.Collections.Generic;
using System.Reflection;

namespace FronkonGames.APECS
{
  /// <summary> Thrown when the system's dependency graph contains a cycle. </summary>
  public sealed class CircularDependencyException : Exception
  {
    public CircularDependencyException(string message) : base(message) { }
  }

  /// <summary>
  /// Discovers, sorts and ticks systems. Reflection runs once during Initialize and the results (system instances,
  /// sorted order, type → system map) are cached in plain fields. The Tick path touches none of that, only the cached lists.
  /// </summary>
  public sealed class SystemScheduler : IDisposable
  {
    private World world;
    private readonly Dictionary<Phase, List<ISystem>> systemsByPhase = new();
    private readonly Dictionary<Type, ISystem> systemsByType = new();
    private bool initialized;
    private bool disposed;

    /// <summary> Total number of registered systems. </summary>
    public int SystemCount => systemsByType.Count;

    /// <summary> Register a new system instance of type <typeparamref name="T"/>. </summary>
    public void Register<T>() where T : ISystem, new()
    {
      ThrowIfInitializedOrDisposed();
      RegisterSystem(new T(), typeof(T));
    }

    /// <summary> Register an existing system instance. </summary>
    public void Register(ISystem system)
    {
      ThrowIfInitializedOrDisposed();
      RegisterSystem(system, system.GetType());
    }

    /// <summary> Remove a previously registered system before <see cref="Initialize"/>. </summary>
    public void Unregister<T>() where T : ISystem
    {
      ThrowIfInitializedOrDisposed();
      var type = typeof(T);
      if (systemsByType.TryGetValue(type, out var system))
      {
        systemsByType.Remove(type);
        foreach (var list in systemsByPhase.Values)
          list.Remove(system);
      }
    }

    /// <summary> Return the registered system of type <typeparamref name="T"/>, or default if absent. </summary>
    public T GetSystem<T>() where T : ISystem
    {
      if (systemsByType.TryGetValue(typeof(T), out var system)) return (T)system;
      return default;
    }

    /// <summary>
    /// Discovers (optionally), sorts and initialises all systems. Call exactly once, after any
    /// explicit <see cref="Register{T}"/> calls and before any Tick.
    /// </summary>
    public void Initialize(World world, bool autoDiscover = true)
    {
      ThrowIfInitializedOrDisposed();

      this.world = world;
      if (autoDiscover) DiscoverAndRegister();

      // Topologically sort each phase and detect cycles.
      foreach (var phase in systemsByPhase.Keys)
        TopologicalSort(systemsByPhase[phase]);

      // Call OnCreate in phase order: Initialization first, Teardown never (no tick).
      if (systemsByPhase.TryGetValue(Phase.Initialization, out var initSystems))
        foreach (var system in initSystems) system.OnCreate(world);

      foreach (var phase in systemsByPhase.Keys)
      {
        if (phase == Phase.Initialization || phase == Phase.Teardown) continue;
        foreach (var system in systemsByPhase[phase]) system.OnCreate(world);
      }

      if (systemsByPhase.TryGetValue(Phase.Teardown, out var teardownSystems))
        foreach (var system in teardownSystems) system.OnCreate(world);

      initialized = true;
    }

    /// <summary> Tick every enabled system registered for <paramref name="phase"/>. </summary>
    public void Tick(Phase phase)
    {
      if (disposed == true)
        return;

      // Flip event queues at the start of the "user-visible" phases so readers see events from the
      // previous phase and writers get a fresh buffer. Other phases (PreRender, Initialization, Teardown) don't swap.
      if (world != null && (phase == Phase.Update || phase == Phase.FixedUpdate || phase == Phase.LateUpdate))
        world.SwapAllEventQueues();

      if (systemsByPhase.TryGetValue(phase, out var list))
        foreach (var system in list)
          if (system.Enabled == true)
            system.OnUpdate(world);
    }

    /// <summary> Call <see cref="ISystem.OnDestroy"/> on all systems in reverse phase order. </summary>
    public void Shutdown()
    {
      if (initialized == false || disposed == true)
        return;

      // Call OnDestroy in reverse phase order; Teardown last so its systems see everything else gone.
      var phases = new List<Phase>(systemsByPhase.Keys);
      for (int i = phases.Count - 1; i >= 0; --i)
      {
        var list = systemsByPhase[phases[i]];
        for (int j = list.Count - 1; j >= 0; --j)
          list[j].OnDestroy(world);
      }
    }

    public void Dispose()
    {
      if (disposed == true)
        return;

      disposed = true;
      Shutdown();
    }

    private void RegisterSystem(ISystem system, Type type)
    {
      if (systemsByType.ContainsKey(type) == true)
        throw new InvalidOperationException($"[APECS] SystemScheduler: system of type {type.FullName} is already registered.");

      systemsByType[type] = system;
      var attr = type.GetCustomAttribute<UpdateInPhaseAttribute>(true);
      var phase = attr?.Phase ?? Phase.Update;
      if (systemsByPhase.ContainsKey(phase) == false)
        systemsByPhase[phase] = new List<ISystem>();

      systemsByPhase[phase].Add(system);
    }

    private void DiscoverAndRegister()
    {
      foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
      {
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types; }
        foreach (var type in types)
        {
          if (type == null)
            continue;

          if (type.IsAbstract == true || type.IsGenericTypeDefinition == true)
            continue;

          if (typeof(ISystem).IsAssignableFrom(type) == false)
            continue;

          var attr = type.GetCustomAttribute<UpdateInPhaseAttribute>(true);
          
          if (attr == null)
            continue; // spec: only ISystem with [UpdateInPhase] are auto-discovered
          try
          {
            var system = (ISystem)Activator.CreateInstance(type);
            RegisterSystem(system, type);
          }
          catch (Exception e)
          {
            throw new InvalidOperationException($"[APECS] SystemScheduler: failed to instantiate system '{type.FullName}': {e.Message}", e);
          }
        }
      }
    }

    private void TopologicalSort(List<ISystem> systems)
    {
      int n = systems.Count;
      if (n <= 1)
        return;

      // Map each system to its position in the input list.
      var indexOf = new Dictionary<ISystem, int>(n);
      for (int i = 0; i < n; ++i)
        indexOf[systems[i]] = i;

      var inDegree = new int[n];
      var outEdges = new List<int>[n];
      for (int i = 0; i < n; ++i)
        outEdges[i] = new List<int>(2);

      // Build edges. If X.UpdateAfter(Y) then Y must run first → edge Y → X.
      // If X.UpdateBefore(Y) then X must run first → edge X → Y.
      for (int i = 0; i < n; ++i)
      {
        var type = systems[i].GetType();

        foreach (var attr in type.GetCustomAttributes<UpdateAfterAttribute>(true))
        {
          if (systemsByType.TryGetValue(attr.SystemType, out var dep))
          {
            if (indexOf.TryGetValue(dep, out int depIdx) && depIdx != i)
            {
              inDegree[i]++;
              outEdges[depIdx].Add(i);
            }
          }
        }

        foreach (var attr in type.GetCustomAttributes<UpdateBeforeAttribute>(true))
        {
          if (systemsByType.TryGetValue(attr.SystemType, out var after))
          {
            if (indexOf.TryGetValue(after, out int afterIdx) && afterIdx != i)
            {
              inDegree[afterIdx]++;
              outEdges[i].Add(afterIdx);
            }
          }
        }
      }

      // Kahn's algorithm.
      var queue = new Queue<int>(n);
      for (int i = 0; i < n; ++i)
        if (inDegree[i] == 0)
          queue.Enqueue(i);

      var sorted = new List<ISystem>(n);
      while (queue.Count > 0)
      {
        int u = queue.Dequeue();
        sorted.Add(systems[u]);
        foreach (var v in outEdges[u])
          if (--inDegree[v] == 0) queue.Enqueue(v);
      }

      if (sorted.Count != n)
        throw new CircularDependencyException("[APECS] SystemScheduler: circular dependency detected in UpdateAfter / UpdateBefore graph. " +
          "Inspect the system's attributes to find the cycle.");

      systems.Clear();
      systems.AddRange(sorted);
    }

    private void ThrowIfInitializedOrDisposed()
    {
      if (disposed == true)
        throw new ObjectDisposedException(nameof(SystemScheduler));

      if (initialized == true)
        throw new InvalidOperationException("[APECS] SystemScheduler: cannot register after Initialize. Call Unregister before Initialize, or Initialize with autoDiscover:false.");
    }
  }
}
