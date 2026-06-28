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
using System.Reflection;

namespace FronkonGames.APECS
{
  /// <summary>
  /// Convenience base class for systems. Subclasses override <see cref="Update"/> (and optionally <see cref="OnInitialize"/> / <see cref="OnShutdown"/>).
  /// </summary>
  public abstract class SystemBase : ISystem
  {
    /// <summary> World this system was created in. </summary>
    protected World World { get; private set; }

    /// <summary> Unity frame delta time, refreshed each tick. </summary>
    protected float DeltaTime { get; private set; }

    /// <summary> Unity fixed timestep delta, refreshed each tick. </summary>
    protected float FixedDeltaTime { get; private set; }

    /// <summary> Start building a query against <see cref="World"/>. </summary>
    protected QueryBuilder Query() => World.Query();

    /// <summary> Return a registered class resource, or <c>null</c> if absent. </summary>
    protected T GetResource<T>() where T : class => World.Resources.Get<T>();

    /// <summary> Return a mutable reference to a registered value resource. </summary>
    protected ref T GetResourceValue<T>() where T : unmanaged => ref World.Resources.GetValue<T>();

    /// <summary> Deferred command buffer owned by this system; flushed at end-of-phase. </summary>
    protected CommandBuffer Commands
    {
      get
      {
        if (World?.CommandBufferSystem == null)
          throw new InvalidOperationException(
            $"{GetType().Name}: no CommandBufferSystem registered on the world. " +
            "Set world.CommandBufferSystem before systems record into Commands.");
        return World.CommandBufferSystem.GetBuffer(this);
      }
    }

    /// <summary> Per-frame event sender for type <typeparamref name="T"/>. </summary>
    protected EventWriter<T> GetEventWriter<T>() where T : unmanaged => new(World.GetEventQueue<T>());

    /// <summary> Per-frame event reader for type <typeparamref name="T"/>. </summary>
    protected EventReader<T> GetEventReader<T>() where T : unmanaged => new(World.GetEventQueue<T>());

    /// <inheritdoc />
    public bool Enabled
    {
      /// <summary> Gets or sets whether this system is ticked by the scheduler. </summary>
      get;
      set;
    } = true;

    void ISystem.OnCreate(World world)
    {
      World = world;
      var attr = GetType().GetCustomAttribute<DisableByDefaultAttribute>(true);
      if (attr != null)
        Enabled = false;

      OnInitialize();
    }

    void ISystem.OnUpdate(World world)
    {
      DeltaTime = UnityEngine.Time.deltaTime;
      FixedDeltaTime = UnityEngine.Time.fixedDeltaTime;

      Update();
    }

    void ISystem.OnDestroy(World world)
    {
      OnShutdown();
    }

    /// <summary> Called once when the scheduler creates this system. </summary>
    protected virtual void OnInitialize() { }

    /// <summary> Called every tick of this system's phase. </summary>
    protected abstract void Update();

    /// <summary> Called once when the scheduler shuts down. </summary>
    protected virtual void OnShutdown() { }
  }
}
