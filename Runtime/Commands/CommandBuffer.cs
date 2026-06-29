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

namespace FronkonGames.APECS
{
  /// <summary>
  /// Per-system (or per-job) buffer for deferred structural mutations. Commands are recorded by value, then applied
  /// to the world during <see cref="Playback"/>, at which point the world's <c>StructuralVersion</c> is bumped
  /// exactly once, so any in-flight query iterators throw on their next MoveNext.
  /// </summary>
  public sealed class CommandBuffer : IDisposable
  {
    /// <summary> High bit of <see cref="Entity.index"/> used to mark a provisional entity. </summary>
    internal const uint ProvisionalPrefix = 0x80000000u;

    private readonly World world;

    private readonly List<ICommand> commands = new();
    private readonly List<Entity> provisional = new();

    private int nextLocalId;
    private bool disposed;

    /// <summary> World this buffer records commands for. </summary>
    public World World => world;

    /// <summary> True when no commands have been recorded. </summary>
    public bool IsEmpty => commands.Count == 0;

    /// <summary> Number of recorded commands awaiting playback. </summary>
    public int CommandCount => commands.Count;

    public CommandBuffer(World world) { this.world = world; }

    // Recording

    /// <summary> Record creation of an entity with an empty component mask. Returns a provisional handle. </summary>
    public Entity CreateEntity() => CreateEntity(default);

    /// <summary> Record creation of an entity with the given component mask. Returns a provisional handle. </summary>
    public Entity CreateEntity(ComponentMask mask)
    {
      int localId = nextLocalId++;
      var e = new Entity(ProvisionalPrefix | (uint)localId, 0u);
      commands.Add(new CreateEntityCommand(localId, mask));

      return e;
    }

    /// <summary> Record adding component <typeparamref name="T"/> to entity <paramref name="e"/>. </summary>
    public void AddComponent<T>(Entity e, T value) where T : unmanaged
    {
      if (typeof(T).IsValueType == false)
        throw new ArgumentException("Component types must be value types.", nameof(T));

      commands.Add(new AddComponentCommand(e, typeof(T), value));
    }

    /// <summary> Record removing component <typeparamref name="T"/> from entity <paramref name="e"/>. </summary>
    public void RemoveComponent<T>(Entity e) where T : unmanaged => commands.Add(new RemoveComponentCommand(e, typeof(T)));

    /// <summary> Record writing component <typeparamref name="T"/> on entity <paramref name="e"/>. </summary>
    public void SetComponent<T>(Entity e, T value) where T : unmanaged => commands.Add(new SetComponentCommand(e, typeof(T), value));

    /// <summary> Record destroying entity <paramref name="e"/>. </summary>
    public void DestroyEntity(Entity e) => commands.Add(new DestroyEntityCommand(e));

    /// <summary> Record destroying every entity that has component <typeparamref name="T"/>. </summary>
    public void DestroyEntitiesWith<T>() where T : unmanaged
    {
      int id = ComponentRegistry.IdOf<T>();
      commands.Add(new DestroyEntitiesWithCommand(id));
    }

    // Provisional resolution

    internal void RegisterProvisional(int localId, Entity realEntity)
    {
      while (provisional.Count <= localId) provisional.Add(default);
      provisional[localId] = realEntity;
    }

    internal Entity Resolve(Entity e)
    {
      if ((e.index & ProvisionalPrefix) == ProvisionalPrefix)
      {
        int localId = (int)(e.index & 0x7FFFFFFFu);
        if (localId >= 0 && localId < provisional.Count)
          return provisional[localId];
      }

      // not a provisional, return as-is
      return e;
    }

    // Playback

    /// <summary> Apply all recorded commands to <paramref name="target"/> and clear the buffer. </summary>
    public void Playback(World target)
    {
      if (commands.Count == 0)
        return;

      provisional.Clear();

      for (int i = 0; i < commands.Count; ++i)
        commands[i].Execute(this, target);

      commands.Clear();
      provisional.Clear();
      target.BumpStructuralVersion();
    }

    /// <summary> Discard all recorded commands and provisional entity mappings. </summary>
    public void Clear()
    {
      commands.Clear();
      provisional.Clear();
    }

    public void Dispose()
    {
      if (disposed) return;
      disposed = true;
      Clear();
    }
  }
}
