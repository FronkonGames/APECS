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
  /// <summary> Top-level ECS container. </summary>
  public sealed class World : IDisposable
  {
    /// <summary> Human-readable world name. </summary>
    public string Name { get; }

    /// <summary> Lazy default world, set by WorldBootstrap. </summary>
    public static World Default
    {
      /// <summary> Gets or sets the process-wide default world. </summary>
      get;
      set;
    }

    /// <summary> Generational entity index and location map. </summary>
    public SparseSet SparseSet
    {
      /// <summary> Gets the sparse set. </summary>
      get;
      private set;
    }

    /// <summary> Owns every archetype in this world. </summary>
    public ArchetypeStorage ArchetypeStorage => archetypeStorage;

    /// <summary> Per-world class and value resources. </summary>
    public ResourceContainer Resources
    {
      /// <summary> Gets the resource container. </summary>
      get;
      private set;
    }

    /// <summary> Set by the bootstrap (or test) so that <see cref="SystemBase.Commands"/> can
    /// route recordings to the correct <see cref="CommandBufferSystem"/>. </summary>
    public CommandBufferSystem CommandBufferSystem
    {
      /// <summary> Gets or sets the world's command-buffer flush system. </summary>
      get;
      set;
    }

    /// <summary> Number of live entities. </summary>
    public int EntityCount => SparseSet?.Count ?? 0;

    /// <summary> Bumped on every structural change (create / destroy / add / remove component). </summary>
    public uint StructuralVersion
    {
      /// <summary> Gets the current structural version. </summary>
      get;
      private set;
    }

    /// <summary> Lazily-created event queues keyed by event type. </summary>
    public IReadOnlyDictionary<System.Type, IEventQueue> EventQueues => eventQueues;

    private ArchetypeStorage archetypeStorage;
    private readonly Dictionary<System.Type, IEventQueue> eventQueues = new();

    /// <summary> Per-world factory registry: Type → (creates a ComponentStore for that Type). </summary>
    readonly Dictionary<Type, Func<IComponentStore>> storeFactories = new();

    bool disposed;

    public World(string name = "World")
    {
      Name = name;
      SparseSet = new SparseSet();
      archetypeStorage = new ArchetypeStorage(ComponentIdToStore);
      Resources = new ResourceContainer();
    }

    // Setup

    /// <summary>
    /// Register a factory that creates a <see cref="ComponentStore{T}"/> for the given component type.
    /// Must be called for every type that will appear in any archetype mask, before entities using that
    /// type are created. Side effect: <typeparamref name="T"/> is also registered in <see cref="ComponentRegistry"/>.
    /// </summary>
    public World RegisterStoreFactory<T>() where T : unmanaged
    {
      ThrowIfDisposed();

      storeFactories[typeof(T)] = () => new ComponentStore<T>(ComponentRegistry.IdOf<T>());

      return this;
    }

    IComponentStore ComponentIdToStore(int componentId)
    {
      var type = ComponentRegistry.TypeOf(componentId);

      if (type == null)
        return null;
      
      if (storeFactories.TryGetValue(type, out var factory) == false)
        return null;
      
      return factory();
    }

    // Entity lifecycle

    /// <summary> Create an entity in the archetype for the given mask (lazily materialised). </summary>
    public Entity CreateEntity(ComponentMask mask)
    {
      ThrowIfDisposed();

      var arch = archetypeStorage.GetOrCreate(mask);
      var e = SparseSet.Create();
      var (chunk, row) = arch.Add(e);

      SparseSet.Set(e, new EntityRecord { ArchetypeId = arch.Id, ChunkIndex = chunk, Row = row });
      BumpStructuralVersion();

      return e;
    }

    /// <summary> Destroy a live entity and remove it from its archetype. No-op for stale or dead handles. </summary>
    public void DestroyEntity(Entity e)
    {
      if (disposed == true || SparseSet.IsAlive(e) == false)
        return;

      if (SparseSet.TryGet(e, out var record) == true)
      {
        var arch = archetypeStorage.GetById(record.ArchetypeId);
        if (arch != null)
        {
          var swappedPrevious = arch.Remove(record.ChunkIndex, record.Row);
          if (swappedPrevious.Row != record.Row)
          {
            // Some other entity was moved into the vacated row. Update its SparseSet entry.
            var chunk = arch.Chunks[record.ChunkIndex];
            var moved = chunk.entities[record.Row];
            SparseSet.Set(moved, new EntityRecord
            {
              ArchetypeId = arch.Id,
              ChunkIndex = record.ChunkIndex,
              Row = record.Row,
            });
          }
        }
      }

      SparseSet.Destroy(e);
      BumpStructuralVersion();
    }

    /// <summary> True when <paramref name="e"/> is a valid, live entity handle. </summary>
    public bool IsAlive(Entity e) => !disposed && SparseSet != null && SparseSet.IsAlive(e);

    // Component access

    /// <summary> Write component <typeparamref name="T"/> on entity <paramref name="e"/>. </summary>
    public void SetComponent<T>(Entity e, T value) where T : unmanaged
    {
      ThrowIfDisposed()
;
      int id = ComponentRegistry.IdOf<T>();
      var record = RequireRecord(e);
      var arch = archetypeStorage.GetById(record.ArchetypeId);

      if (arch == null || arch.HasComponent(id) == false)
        throw new InvalidOperationException($"[APECS] World.SetComponent<{typeof(T).Name}>: entity {e} does not have this component.");
      
      var store = (ComponentStore<T>)arch.GetStore(id);
      store.Set(record.ChunkIndex, record.Row, value);
    }

    /// <summary> Read component <typeparamref name="T"/> from entity <paramref name="e"/>. </summary>
    public T GetComponent<T>(Entity e) where T : unmanaged
    {
      ThrowIfDisposed();

      int id = ComponentRegistry.IdOf<T>();
      var record = RequireRecord(e);
      var arch = archetypeStorage.GetById(record.ArchetypeId);
      
      if (arch == null || arch.HasComponent(id) == false)
        throw new InvalidOperationException($"[APECS] World.GetComponent<{typeof(T).Name}>: entity {e} does not have this component.");

      var store = (ComponentStore<T>)arch.GetStore(id);
      return store.Get(record.ChunkIndex, record.Row);
    }

    /// <summary> Return a mutable reference to component <typeparamref name="T"/> on entity <paramref name="e"/>. </summary>
    public ref T GetComponentRef<T>(Entity e) where T : unmanaged
    {
      ThrowIfDisposed();

      int id = ComponentRegistry.IdOf<T>();
      var record = RequireRecord(e);
      var arch = archetypeStorage.GetById(record.ArchetypeId);
      
      if (arch == null || arch.HasComponent(id) == false)
        throw new InvalidOperationException($"[APECS] World.GetComponent<{typeof(T).Name}>: entity {e} does not have this component.");

      var store = (ComponentStore<T>)arch.GetStore(id);
      return ref store.GetRef(record.ChunkIndex, record.Row);
    }

    /// <summary> True when entity <paramref name="e"/> currently has component <typeparamref name="T"/>. </summary>
    public bool HasComponent<T>(Entity e) where T : unmanaged
    {
      if (disposed == true)
        return false;

      int id = ComponentRegistry.IdOf<T>();

      if (SparseSet.IsAlive(e) == false)
        return false;

      if (SparseSet.TryGet(e, out var record) == false)
        return false;

      var arch = archetypeStorage.GetById(record.ArchetypeId);
      return arch != null && arch.HasComponent(id);
    }

    // Structural mutations

    /// <summary> Move the entity into the new archetype that includes component T. </summary>
    public void AddComponent<T>(Entity e, T value) where T : unmanaged
    {
      ThrowIfDisposed();

      int id = ComponentRegistry.IdOf<T>();
      var oldRecord = RequireRecord(e);
      var oldArch = archetypeStorage.GetById(oldRecord.ArchetypeId);
      if (oldArch == null)
        throw new InvalidOperationException($"[APECS] World.AddComponent<{typeof(T).Name}>: entity {e} has no archetype.");

      // Build the new mask: old mask | {T}
      var newMask = oldArch.Mask;
      newMask.Set(id);
      var newArch = archetypeStorage.GetOrCreate(newMask);

      if (oldArch == newArch)
      {
        // T is already in the mask; no migration. Just write the value.
        var store = (ComponentStore<T>)oldArch.GetStore(id);
        store.Set(oldRecord.ChunkIndex, oldRecord.Row, value);
        return;
      }

      var (newChunk, newRow) = newArch.AddWithCopy(e, oldArch, oldRecord.ChunkIndex, oldRecord.Row);
      RemoveFromArchetypeAndFixSwap(oldArch, oldRecord);
      SparseSet.Set(e, new EntityRecord { ArchetypeId = newArch.Id, ChunkIndex = newChunk, Row = newRow });

      // Write the value of T in the new archetype (it was not present in the source).
      var newStore = (ComponentStore<T>)newArch.GetStore(id);
      newStore.Set(newChunk, newRow, value);

      BumpStructuralVersion();
    }

    /// <summary> Move the entity into the new archetype that lacks component T. No-op if T is not present. </summary>
    public void RemoveComponent<T>(Entity e) where T : unmanaged
    {
      ThrowIfDisposed();

      int id = ComponentRegistry.IdOf<T>();
      var oldRecord = RequireRecord(e);
      var oldArch = archetypeStorage.GetById(oldRecord.ArchetypeId);

      if (oldArch == null)
        throw new InvalidOperationException($"[APECS] World.RemoveComponent<{typeof(T).Name}>: entity {e} has no archetype.");

      if (oldArch.Mask.Has(id) == false) // T not present, no-op
        return;

      // Build the new mask: old mask & ~{T}
      var newMask = oldArch.Mask;
      newMask.Unset(id);
      var newArch = archetypeStorage.GetOrCreate(newMask);

      var (newChunk, newRow) = newArch.AddWithCopy(e, oldArch, oldRecord.ChunkIndex, oldRecord.Row);
      RemoveFromArchetypeAndFixSwap(oldArch, oldRecord);
      SparseSet.Set(e, new EntityRecord { ArchetypeId = newArch.Id, ChunkIndex = newChunk, Row = newRow });

      BumpStructuralVersion();
    }

    void RemoveFromArchetypeAndFixSwap(Archetype arch, EntityRecord record)
    {
      var swappedPrevious = arch.Remove(record.ChunkIndex, record.Row);
      if (swappedPrevious.Row != record.Row)
      {
        var chunk = arch.Chunks[record.ChunkIndex];
        var moved = chunk.entities[record.Row];
        SparseSet.Set(moved, new EntityRecord
        {
          ArchetypeId = arch.Id,
          ChunkIndex = record.ChunkIndex,
          Row = record.Row,
        });
      }
    }

    // Queries

    /// <summary> Start building an entity query against this world. </summary>
    public QueryBuilder Query()
    {
      ThrowIfDisposed();

      return new QueryBuilder(this);
    }

    // Versioning

    /// <summary> Increment <see cref="StructuralVersion"/> after a structural change. </summary>
    public void BumpStructuralVersion() => StructuralVersion++;

    // Internals

    private EntityRecord RequireRecord(Entity e)
    {
      if (SparseSet.IsAlive(e) == false)
        throw new InvalidOperationException($"[APECS] World: entity {e} is not alive.");

      return SparseSet.Get(e);
    }

    // Event queues

    /// <summary> Returns (lazily creating) the event queue for type <typeparamref name="T"/>. </summary>
    public EventQueue<T> GetEventQueue<T>() where T : unmanaged
    {
      if (eventQueues.TryGetValue(typeof(T), out var q) == false)
      {
        q = new EventQueue<T>();
        eventQueues[typeof(T)] = q;
      }

      return (EventQueue<T>)q;
    }

    /// <summary> Flips every registered event queue's write/read side. Called by the
    /// scheduler at the start of <see cref="Phase.Update"/>, <see cref="Phase.FixedUpdate"/>
    /// and <see cref="Phase.LateUpdate"/>. </summary>
    public void SwapAllEventQueues()
    {
      foreach (var q in eventQueues.Values) q.Swap();
    }

    /// <summary> Clear every registered event queue without disposing them. </summary>
    public void ClearAllEventQueues()
    {
      foreach (var q in eventQueues.Values) q.Clear();
    }

    // ───────────────────────── Lifecycle ─────────────────────────

    public void Dispose()
    {
      if (disposed == true)
        return;
      
      disposed = true;
      archetypeStorage?.Dispose();
      archetypeStorage = null;
      SparseSet?.Dispose();
      SparseSet = null;
      Resources?.Dispose();
      Resources = null;

      foreach (var q in eventQueues.Values)
        q.Dispose();

      eventQueues.Clear();

      if (ReferenceEquals(Default, this) == true)
        Default = null;
    }

    private void ThrowIfDisposed()
    {
      if (disposed == true)
        throw new ObjectDisposedException(nameof(World));
    }
  }
}
