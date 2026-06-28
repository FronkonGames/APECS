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
using Unity.Collections;

namespace FronkonGames.APECS
{
  /// <summary> Per-iterator callback that takes a ref to a component. </summary>
  /// <param name="entity"> Entity being visited. </param>
  /// <param name="component"> Mutable component reference for that entity. </param>
  public delegate void EntityComponentRefAction<T>(Entity entity, ref T component);

  /// <summary>
  /// One entry yielded by a <see cref="QueryIterator{T}"/>. Lives on the stack; the <see cref="Component"/> property
  /// returns a <c>ref</c> straight into the matching chunk's store, so writes mutate the entity's data in place.
  /// We avoid storing the ref as a field (a C# 11 preview feature) and compute it on access instead.
  /// </summary>
  public ref struct QueryEntry<T> where T : unmanaged
  {
    /// <summary> Entity handle for the current row. </summary>
    public Entity entity;

    internal ComponentStore<T> store;
    internal int chunkIndex;
    internal int row;

    internal QueryEntry(Entity entity, ComponentStore<T> store, int chunkIndex, int row)
    {
      this.entity = entity;
      this.store = store;
      this.chunkIndex = chunkIndex;
      this.row = row;
    }

    /// <summary> Mutable reference to component <typeparamref name="T"/> for the current row. </summary>
    public readonly ref T Component => ref store.GetRef(chunkIndex, row);
  }

  /// <summary>
  /// Shared iteration state and MoveNext logic. Each typed <c>QueryIterator<T...></c> holds one of these
  /// and adapts Current / ForEach / GetComponentArray to its types.
  /// </summary>
  internal struct QueryIteratorState
  {
    public World world;
    public QueryFilter filter;
    public int[] componentIds;
    public uint version;
    public int archetypeCount;
    public int archetypeIndex;
    public int chunkIndex;
    public int row;
    public Archetype currentArchetype;
    public ArchetypeChunk currentChunk;
    public int currentChunkCount;

    public readonly bool Matches(Archetype arch)
    {
      if (filter.Matches(arch.Mask) == false)
        return false;

      for (int i = 0; i < componentIds.Length; ++i)
      {
        if (arch.HasComponent(componentIds[i]) == false)
          return false;
      }

      return true;
    }

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (world.StructuralVersion != version)
        throw new InvalidOperationException("QueryIterator: world structural change during iteration. Capture a new iterator after structural mutations.");

      while (true)
      {
        if (currentArchetype != null && currentChunkCount > 0)
        {
          row++;
          if (row < currentChunkCount)
            return true;
        }

        if (currentArchetype != null)
        {
          chunkIndex++;
          if (chunkIndex < currentArchetype.ChunkCount)
          {
            currentChunk = currentArchetype.Chunks[chunkIndex];
            currentChunkCount = currentChunk.count;
            row = 0;
            if (currentChunkCount > 0)
              return true;

            continue;
          }
        }

        archetypeIndex++;
        if (archetypeIndex >= archetypeCount) return false;

        var arch = world.ArchetypeStorage.All[archetypeIndex];
        if (!Matches(arch)) continue;
        if (arch.ChunkCount == 0) continue;

        currentArchetype = arch;
        chunkIndex = 0;
        currentChunk = arch.Chunks[0];
        currentChunkCount = currentChunk.count;
        row = 0;
        if (currentChunkCount > 0) return true;
      }
    }

    public int LiveCount()
    {
      int count = 0;
      int n = world.ArchetypeStorage.ArchetypeCount;
      for (int i = 0; i < n; i++)
      {
        var arch = world.ArchetypeStorage.All[i];
        if (Matches(arch)) count += arch.EntityCount;
      }
      return count;
    }

    public int LiveMatchingArchetypeCount()
    {
      int count = 0;
      int n = world.ArchetypeStorage.ArchetypeCount;
      for (int i = 0; i < n; i++)
        if (Matches(world.ArchetypeStorage.All[i])) count++;
      return count;
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // 1 component
  // ════════════════════════════════════════════════════════════════════════

  public ref struct QueryIterator<T> where T : unmanaged
  {
    internal QueryIteratorState state;
    internal ComponentStore<T> currentStore;
    internal int componentId;
    internal Entity currentEntity;

    internal QueryIterator(World world, QueryFilter filter)
    {
      componentId = ComponentRegistry.IdOf<T>();
      state = new QueryIteratorState
      {
        world = world,
        filter = filter,
        componentIds = new int[] { componentId },
        version = world.StructuralVersion,
        archetypeCount = world.ArchetypeStorage.ArchetypeCount,
        archetypeIndex = -1,
        chunkIndex = 0,
        row = 0,
        currentArchetype = null,
        currentChunkCount = 0,
      };
      currentStore = null;
      currentEntity = default;
    }

    /// <summary> Duck-typed foreach support. </summary>
    public QueryIterator<T> GetEnumerator() => this;

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (state.MoveNext() == false)
        return false;

      currentStore = (ComponentStore<T>)state.currentArchetype.GetStore(componentId);
      currentEntity = state.currentChunk.entities[state.row];

      return true;
    }

    /// <summary> Current query entry (valid after a successful <see cref="MoveNext"/>). </summary>
    public QueryEntry<T> Current => new QueryEntry<T>(currentEntity, currentStore, state.chunkIndex, state.row);

    /// <summary> Number of live entities matching this query (full scan). </summary>
    public int Count => state.LiveCount();

    /// <summary> Number of archetypes matching this query (full scan). </summary>
    public int MatchingArchetypeCount => state.LiveMatchingArchetypeCount();

    /// <summary> Invoke <paramref name="fn"/> for each entity, passing a ref to component <typeparamref name="T"/>. </summary>
    public void ForEach(EntityComponentRefAction<T> fn)
    {
      while (MoveNext())
        fn(currentEntity, ref currentStore.GetRef(state.chunkIndex, state.row));
    }

    /// <summary> Dense native slice of component <typeparamref name="T"/> for one chunk (live rows only). </summary>
    public NativeArray<T> GetComponentArray(int archetypeIndex, int chunkIndex)
    {
      var arch = state.world.ArchetypeStorage.All[archetypeIndex];
      var store = (ComponentStore<T>)arch.GetStore(componentId);

      return store.GetChunkArray(chunkIndex, arch.Chunks[chunkIndex].count);
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // 2 components
  // ════════════════════════════════════════════════════════════════════════

  public ref struct QueryEntry<T, U> where T : unmanaged where U : unmanaged
  {
    /// <summary> Entity handle for the current row. </summary>
    public Entity entity;
    internal ComponentStore<T> storeT;
    internal ComponentStore<U> storeU;
    internal int chunkIndex;
    internal int row;

    internal QueryEntry(Entity entity, ComponentStore<T> storeT, ComponentStore<U> storeU, int chunkIndex, int row)
    {
      this.entity = entity;
      this.storeT = storeT;
      this.storeU = storeU;
      this.chunkIndex = chunkIndex;
      this.row = row;
    }

    /// <summary> Mutable reference to component <typeparamref name="T"/> for the current row. </summary>
    public ref T t => ref storeT.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="U"/> for the current row. </summary>
    public ref U u => ref storeU.GetRef(chunkIndex, row);
  }

  public ref struct QueryIterator<T, U> where T : unmanaged where U : unmanaged
  {
    internal QueryIteratorState state;
    internal ComponentStore<T> currentStoreT;
    internal ComponentStore<U> currentStoreU;
    internal int componentIdT;
    internal int componentIdU;
    internal Entity currentEntity;

    internal QueryIterator(World world, QueryFilter filter)
    {
      componentIdT = ComponentRegistry.IdOf<T>();
      componentIdU = ComponentRegistry.IdOf<U>();
      state = new QueryIteratorState
      {
        world = world,
        filter = filter,
        componentIds = new int[] { componentIdT, componentIdU },
        version = world.StructuralVersion,
        archetypeCount = world.ArchetypeStorage.ArchetypeCount,
        archetypeIndex = -1,
        chunkIndex = 0,
        row = 0,
        currentArchetype = null,
        currentChunkCount = 0,
      };
      currentStoreT = null;
      currentStoreU = null;
      currentEntity = default;
    }

    /// <summary> Duck-typed foreach support. </summary>
    public QueryIterator<T, U> GetEnumerator() => this;

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (state.MoveNext() == false)
        return false;

      currentStoreT = (ComponentStore<T>)state.currentArchetype.GetStore(componentIdT);
      currentStoreU = (ComponentStore<U>)state.currentArchetype.GetStore(componentIdU);
      currentEntity = state.currentChunk.entities[state.row];

      return true;
    }

    /// <summary> Current query entry (valid after a successful <see cref="MoveNext"/>). </summary>
    public QueryEntry<T, U> Current => new QueryEntry<T, U>(currentEntity, currentStoreT, currentStoreU, state.chunkIndex, state.row);

    /// <summary> Number of live entities matching this query (full scan). </summary>
    public int Count => state.LiveCount();

    /// <summary> Number of archetypes matching this query (full scan). </summary>
    public int MatchingArchetypeCount => state.LiveMatchingArchetypeCount();

    /// <summary> Invoke <paramref name="fn"/> for each entity, passing a ref to component <typeparamref name="T"/>. </summary>
    public void ForEach(EntityComponentRefAction<T> fn)
    {
      while (MoveNext()) fn(currentEntity, ref currentStoreT.GetRef(state.chunkIndex, state.row));
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // 3 components
  // ════════════════════════════════════════════════════════════════════════

  public ref struct QueryEntry<T, U, V> where T : unmanaged where U : unmanaged where V : unmanaged
  {
    /// <summary> Entity handle for the current row. </summary>
    public Entity entity;
    internal ComponentStore<T> storeT;
    internal ComponentStore<U> storeU;
    internal ComponentStore<V> storeV;
    internal int chunkIndex;
    internal int row;

    internal QueryEntry(Entity entity, ComponentStore<T> storeT, ComponentStore<U> storeU, ComponentStore<V> storeV, int chunkIndex, int row)
    {
      this.entity = entity;
      this.storeT = storeT;
      this.storeU = storeU;
      this.storeV = storeV;
      this.chunkIndex = chunkIndex;
      this.row = row;
    }

    /// <summary> Mutable reference to component <typeparamref name="T"/> for the current row. </summary>
    public ref T t => ref storeT.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="U"/> for the current row. </summary>
    public ref U u => ref storeU.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="V"/> for the current row. </summary>
    public ref V v => ref storeV.GetRef(chunkIndex, row);
  }

  public ref struct QueryIterator<T, U, V> where T : unmanaged where U : unmanaged where V : unmanaged
  {
    internal QueryIteratorState state;
    internal ComponentStore<T> currentStoreT;
    internal ComponentStore<U> currentStoreU;
    internal ComponentStore<V> currentStoreV;
    internal int componentIdT;
    internal int componentIdU;
    internal int componentIdV;
    internal Entity currentEntity;

    internal QueryIterator(World world, QueryFilter filter)
    {
      componentIdT = ComponentRegistry.IdOf<T>();
      componentIdU = ComponentRegistry.IdOf<U>();
      componentIdV = ComponentRegistry.IdOf<V>();
      state = new QueryIteratorState
      {
        world = world,
        filter = filter,
        componentIds = new int[] { componentIdT, componentIdU, componentIdV },
        version = world.StructuralVersion,
        archetypeCount = world.ArchetypeStorage.ArchetypeCount,
        archetypeIndex = -1,
        chunkIndex = 0,
        row = 0,
        currentArchetype = null,
        currentChunkCount = 0,
      };
      currentStoreT = null;
      currentStoreU = null;
      currentStoreV = null;
      currentEntity = default;
    }

    /// <summary> Duck-typed foreach support. </summary>
    public QueryIterator<T, U, V> GetEnumerator() => this;

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (state.MoveNext() == false)
        return false;
        
      var arch = state.currentArchetype;
      currentStoreT = (ComponentStore<T>)arch.GetStore(componentIdT);
      currentStoreU = (ComponentStore<U>)arch.GetStore(componentIdU);
      currentStoreV = (ComponentStore<V>)arch.GetStore(componentIdV);
      currentEntity = state.currentChunk.entities[state.row];

      return true;
    }

    /// <summary> Current query entry (valid after a successful <see cref="MoveNext"/>). </summary>
    public QueryEntry<T, U, V> Current => new QueryEntry<T, U, V>(currentEntity, currentStoreT, currentStoreU, currentStoreV, state.chunkIndex, state.row);

    /// <summary> Number of live entities matching this query (full scan). </summary>
    public int Count => state.LiveCount();

    /// <summary> Number of archetypes matching this query (full scan). </summary>
    public int MatchingArchetypeCount => state.LiveMatchingArchetypeCount();

    /// <summary> Invoke <paramref name="fn"/> for each entity, passing a ref to component <typeparamref name="T"/>. </summary>
    public void ForEach(EntityComponentRefAction<T> fn)
    {
      while (MoveNext()) fn(currentEntity, ref currentStoreT.GetRef(state.chunkIndex, state.row));
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // 4 components
  // ════════════════════════════════════════════════════════════════════════

  public ref struct QueryEntry<T, U, V, W>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
  {
    /// <summary> Entity handle for the current row. </summary>
    public Entity entity;
    internal ComponentStore<T> storeT;
    internal ComponentStore<U> storeU;
    internal ComponentStore<V> storeV;
    internal ComponentStore<W> storeW;
    internal int chunkIndex;
    internal int row;

    internal QueryEntry(Entity entity, ComponentStore<T> storeT, ComponentStore<U> storeU, ComponentStore<V> storeV, ComponentStore<W> storeW, int chunkIndex, int row)
    {
      this.entity = entity;
      this.storeT = storeT;
      this.storeU = storeU;
      this.storeV = storeV;
      this.storeW = storeW;
      this.chunkIndex = chunkIndex;
      this.row = row;
    }

    /// <summary> Mutable reference to component <typeparamref name="T"/> for the current row. </summary>
    public ref T t => ref storeT.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="U"/> for the current row. </summary>
    public ref U u => ref storeU.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="V"/> for the current row. </summary>
    public ref V v => ref storeV.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="W"/> for the current row. </summary>
    public ref W w => ref storeW.GetRef(chunkIndex, row);
  }

  public ref struct QueryIterator<T, U, V, W>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
  {
    internal QueryIteratorState state;
    internal ComponentStore<T> currentStoreT;
    internal ComponentStore<U> currentStoreU;
    internal ComponentStore<V> currentStoreV;
    internal ComponentStore<W> currentStoreW;
    internal int componentIdT;
    internal int componentIdU;
    internal int componentIdV;
    internal int componentIdW;
    internal Entity currentEntity;

    internal QueryIterator(World world, QueryFilter filter)
    {
      componentIdT = ComponentRegistry.IdOf<T>();
      componentIdU = ComponentRegistry.IdOf<U>();
      componentIdV = ComponentRegistry.IdOf<V>();
      componentIdW = ComponentRegistry.IdOf<W>();
      state = new QueryIteratorState
      {
        world = world,
        filter = filter,
        componentIds = new int[] { componentIdT, componentIdU, componentIdV, componentIdW },
        version = world.StructuralVersion,
        archetypeCount = world.ArchetypeStorage.ArchetypeCount,
        archetypeIndex = -1,
        chunkIndex = 0,
        row = 0,
        currentArchetype = null,
        currentChunkCount = 0,
      };
      currentStoreT = null;
      currentStoreU = null;
      currentStoreV = null;
      currentStoreW = null;
      currentEntity = default;
    }

    /// <summary> Duck-typed foreach support. </summary>
    public QueryIterator<T, U, V, W> GetEnumerator() => this;

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (state.MoveNext() == false)
        return false;
        
      var arch = state.currentArchetype;
      currentStoreT = (ComponentStore<T>)arch.GetStore(componentIdT);
      currentStoreU = (ComponentStore<U>)arch.GetStore(componentIdU);
      currentStoreV = (ComponentStore<V>)arch.GetStore(componentIdV);
      currentStoreW = (ComponentStore<W>)arch.GetStore(componentIdW);
      currentEntity = state.currentChunk.entities[state.row];

      return true;
    }

    /// <summary> Current query entry (valid after a successful <see cref="MoveNext"/>). </summary>
    public QueryEntry<T, U, V, W> Current => new QueryEntry<T, U, V, W>(currentEntity, currentStoreT, currentStoreU, currentStoreV, currentStoreW, state.chunkIndex, state.row);

    /// <summary> Number of live entities matching this query (full scan). </summary>
    public int Count => state.LiveCount();

    /// <summary> Number of archetypes matching this query (full scan). </summary>
    public int MatchingArchetypeCount => state.LiveMatchingArchetypeCount();

    /// <summary> Invoke <paramref name="fn"/> for each entity, passing a ref to component <typeparamref name="T"/>. </summary>
    public void ForEach(EntityComponentRefAction<T> fn)
    {
      while (MoveNext()) fn(currentEntity, ref currentStoreT.GetRef(state.chunkIndex, state.row));
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // 5 components
  // ════════════════════════════════════════════════════════════════════════

  public ref struct QueryEntry<T, U, V, W, X>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged where X : unmanaged
  {
    /// <summary> Entity handle for the current row. </summary>
    public Entity entity;
    internal ComponentStore<T> storeT;
    internal ComponentStore<U> storeU;
    internal ComponentStore<V> storeV;
    internal ComponentStore<W> storeW;
    internal ComponentStore<X> storeX;
    internal int chunkIndex;
    internal int row;

    internal QueryEntry(Entity entity, ComponentStore<T> storeT, ComponentStore<U> storeU, ComponentStore<V> storeV, ComponentStore<W> storeW, ComponentStore<X> storeX, int chunkIndex, int row)
    {
      this.entity = entity;
      this.storeT = storeT;
      this.storeU = storeU;
      this.storeV = storeV;
      this.storeW = storeW;
      this.storeX = storeX;
      this.chunkIndex = chunkIndex;
      this.row = row;
    }

    /// <summary> Mutable reference to component <typeparamref name="T"/> for the current row. </summary>
    public ref T t => ref storeT.GetRef(chunkIndex, row);
    
    /// <summary> Mutable reference to component <typeparamref name="U"/> for the current row. </summary>
    public ref U u => ref storeU.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="V"/> for the current row. </summary>
    public ref V v => ref storeV.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="W"/> for the current row. </summary>
    public ref W w => ref storeW.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="X"/> for the current row. </summary>
    public ref X x => ref storeX.GetRef(chunkIndex, row);
  }

  public ref struct QueryIterator<T, U, V, W, X>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged where X : unmanaged
  {
    internal QueryIteratorState state;
    internal ComponentStore<T> currentStoreT;
    internal ComponentStore<U> currentStoreU;
    internal ComponentStore<V> currentStoreV;
    internal ComponentStore<W> currentStoreW;
    internal ComponentStore<X> currentStoreX;
    internal int componentIdT;
    internal int componentIdU;
    internal int componentIdV;
    internal int componentIdW;
    internal int componentIdX;
    internal Entity currentEntity;

    internal QueryIterator(World world, QueryFilter filter)
    {
      componentIdT = ComponentRegistry.IdOf<T>();
      componentIdU = ComponentRegistry.IdOf<U>();
      componentIdV = ComponentRegistry.IdOf<V>();
      componentIdW = ComponentRegistry.IdOf<W>();
      componentIdX = ComponentRegistry.IdOf<X>();
      state = new QueryIteratorState
      {
        world = world,
        filter = filter,
        componentIds = new int[] { componentIdT, componentIdU, componentIdV, componentIdW, componentIdX },
        version = world.StructuralVersion,
        archetypeCount = world.ArchetypeStorage.ArchetypeCount,
        archetypeIndex = -1,
        chunkIndex = 0,
        row = 0,
        currentArchetype = null,
        currentChunkCount = 0,
      };
      currentStoreT = null;
      currentStoreU = null;
      currentStoreV = null;
      currentStoreW = null;
      currentStoreX = null;
      currentEntity = default;
    }

    /// <summary> Duck-typed foreach support. </summary>
    public QueryIterator<T, U, V, W, X> GetEnumerator() => this;

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (state.MoveNext() == false)
        return false;
        
      var arch = state.currentArchetype;
      currentStoreT = (ComponentStore<T>)arch.GetStore(componentIdT);
      currentStoreU = (ComponentStore<U>)arch.GetStore(componentIdU);
      currentStoreV = (ComponentStore<V>)arch.GetStore(componentIdV);
      currentStoreW = (ComponentStore<W>)arch.GetStore(componentIdW);
      currentStoreX = (ComponentStore<X>)arch.GetStore(componentIdX);
      currentEntity = state.currentChunk.entities[state.row];

      return true;
    }

    /// <summary> Current query entry (valid after a successful <see cref="MoveNext"/>). </summary>
    public QueryEntry<T, U, V, W, X> Current => new QueryEntry<T, U, V, W, X>(currentEntity, currentStoreT, currentStoreU, currentStoreV, currentStoreW, currentStoreX, state.chunkIndex, state.row);

    /// <summary> Number of live entities matching this query (full scan). </summary>
    public int Count => state.LiveCount();

    /// <summary> Number of archetypes matching this query (full scan). </summary>
    public int MatchingArchetypeCount => state.LiveMatchingArchetypeCount();

    /// <summary> Invoke <paramref name="fn"/> for each entity, passing a ref to component <typeparamref name="T"/>. </summary>
    public void ForEach(EntityComponentRefAction<T> fn)
    {
      while (MoveNext()) fn(currentEntity, ref currentStoreT.GetRef(state.chunkIndex, state.row));
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // 6 components
  // ════════════════════════════════════════════════════════════════════════

  public ref struct QueryEntry<T, U, V, W, X, Y>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
    where X : unmanaged where Y : unmanaged
  {
    /// <summary> Entity handle for the current row. </summary>
    public Entity entity;
    internal ComponentStore<T> storeT;
    internal ComponentStore<U> storeU;
    internal ComponentStore<V> storeV;
    internal ComponentStore<W> storeW;
    internal ComponentStore<X> storeX;
    internal ComponentStore<Y> storeY;
    internal int chunkIndex;
    internal int row;

    internal QueryEntry(Entity entity, ComponentStore<T> storeT, ComponentStore<U> storeU, ComponentStore<V> storeV, ComponentStore<W> storeW, ComponentStore<X> storeX, ComponentStore<Y> storeY, int chunkIndex, int row)
    {
      this.entity = entity;
      this.storeT = storeT;
      this.storeU = storeU;
      this.storeV = storeV;
      this.storeW = storeW;
      this.storeX = storeX;
      this.storeY = storeY;
      this.chunkIndex = chunkIndex;
      this.row = row;
    }

    /// <summary> Mutable reference to component <typeparamref name="T"/> for the current row. </summary>
    public ref T t => ref storeT.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="U"/> for the current row. </summary>
    public ref U u => ref storeU.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="V"/> for the current row. </summary>
    public ref V v => ref storeV.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="W"/> for the current row. </summary>
    public ref W w => ref storeW.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="X"/> for the current row. </summary>
    public ref X x => ref storeX.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="Y"/> for the current row. </summary>
    public ref Y y => ref storeY.GetRef(chunkIndex, row);
  }

  public ref struct QueryIterator<T, U, V, W, X, Y>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
    where X : unmanaged where Y : unmanaged
  {
    internal QueryIteratorState state;
    internal ComponentStore<T> currentStoreT;
    internal ComponentStore<U> currentStoreU;
    internal ComponentStore<V> currentStoreV;
    internal ComponentStore<W> currentStoreW;
    internal ComponentStore<X> currentStoreX;
    internal ComponentStore<Y> currentStoreY;
    internal int componentIdT;
    internal int componentIdU;
    internal int componentIdV;
    internal int componentIdW;
    internal int componentIdX;
    internal int componentIdY;
    internal Entity currentEntity;

    internal QueryIterator(World world, QueryFilter filter)
    {
      componentIdT = ComponentRegistry.IdOf<T>();
      componentIdU = ComponentRegistry.IdOf<U>();
      componentIdV = ComponentRegistry.IdOf<V>();
      componentIdW = ComponentRegistry.IdOf<W>();
      componentIdX = ComponentRegistry.IdOf<X>();
      componentIdY = ComponentRegistry.IdOf<Y>();
      state = new QueryIteratorState
      {
        world = world,
        filter = filter,
        componentIds = new int[] { componentIdT, componentIdU, componentIdV, componentIdW, componentIdX, componentIdY },
        version = world.StructuralVersion,
        archetypeCount = world.ArchetypeStorage.ArchetypeCount,
        archetypeIndex = -1,
        chunkIndex = 0,
        row = 0,
        currentArchetype = null,
        currentChunkCount = 0,
      };
      currentStoreT = null;
      currentStoreU = null;
      currentStoreV = null;
      currentStoreW = null;
      currentStoreX = null;
      currentStoreY = null;
      currentEntity = default;
    }

    /// <summary> Duck-typed foreach support. </summary>
    public QueryIterator<T, U, V, W, X, Y> GetEnumerator() => this;

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (state.MoveNext() == false)
        return false;
        
      var arch = state.currentArchetype;
      currentStoreT = (ComponentStore<T>)arch.GetStore(componentIdT);
      currentStoreU = (ComponentStore<U>)arch.GetStore(componentIdU);
      currentStoreV = (ComponentStore<V>)arch.GetStore(componentIdV);
      currentStoreW = (ComponentStore<W>)arch.GetStore(componentIdW);
      currentStoreX = (ComponentStore<X>)arch.GetStore(componentIdX);
      currentStoreY = (ComponentStore<Y>)arch.GetStore(componentIdY);
      currentEntity = state.currentChunk.entities[state.row];

      return true;
    }

    /// <summary> Current query entry (valid after a successful <see cref="MoveNext"/>). </summary>
    public QueryEntry<T, U, V, W, X, Y> Current => new QueryEntry<T, U, V, W, X, Y>(currentEntity, currentStoreT, currentStoreU, currentStoreV, currentStoreW, currentStoreX, currentStoreY, state.chunkIndex, state.row);

    /// <summary> Number of live entities matching this query (full scan). </summary>
    public int Count => state.LiveCount();

    /// <summary> Number of archetypes matching this query (full scan). </summary>
    public int MatchingArchetypeCount => state.LiveMatchingArchetypeCount();

    /// <summary> Invoke <paramref name="fn"/> for each entity, passing a ref to component <typeparamref name="T"/>. </summary>
    public void ForEach(EntityComponentRefAction<T> fn)
    {
      while (MoveNext()) fn(currentEntity, ref currentStoreT.GetRef(state.chunkIndex, state.row));
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // 7 components
  // ════════════════════════════════════════════════════════════════════════

  public ref struct QueryEntry<T, U, V, W, X, Y, Z>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
    where X : unmanaged where Y : unmanaged where Z : unmanaged
  {
    /// <summary> Entity handle for the current row. </summary>
    public Entity entity;
    internal ComponentStore<T> storeT;
    internal ComponentStore<U> storeU;
    internal ComponentStore<V> storeV;
    internal ComponentStore<W> storeW;
    internal ComponentStore<X> storeX;
    internal ComponentStore<Y> storeY;
    internal ComponentStore<Z> storeZ;
    internal int chunkIndex;
    internal int row;

    internal QueryEntry(Entity entity, ComponentStore<T> storeT, ComponentStore<U> storeU, ComponentStore<V> storeV, ComponentStore<W> storeW, ComponentStore<X> storeX, ComponentStore<Y> storeY, ComponentStore<Z> storeZ, int chunkIndex, int row)
    {
      this.entity = entity;
      this.storeT = storeT;
      this.storeU = storeU;
      this.storeV = storeV;
      this.storeW = storeW;
      this.storeX = storeX;
      this.storeY = storeY;
      this.storeZ = storeZ;
      this.chunkIndex = chunkIndex;
      this.row = row;
    }

    /// <summary> Mutable reference to component <typeparamref name="T"/> for the current row. </summary>
    public ref T t => ref storeT.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="U"/> for the current row. </summary>
    public ref U u => ref storeU.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="V"/> for the current row. </summary>
    public ref V v => ref storeV.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="W"/> for the current row. </summary>
    public ref W w => ref storeW.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="X"/> for the current row. </summary>
    public ref X x => ref storeX.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="Y"/> for the current row. </summary>
    public ref Y y => ref storeY.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="Z"/> for the current row. </summary>
    public ref Z z => ref storeZ.GetRef(chunkIndex, row);
  }

  public ref struct QueryIterator<T, U, V, W, X, Y, Z>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
    where X : unmanaged where Y : unmanaged where Z : unmanaged
  {
    internal QueryIteratorState state;
    internal ComponentStore<T> currentStoreT;
    internal ComponentStore<U> currentStoreU;
    internal ComponentStore<V> currentStoreV;
    internal ComponentStore<W> currentStoreW;
    internal ComponentStore<X> currentStoreX;
    internal ComponentStore<Y> currentStoreY;
    internal ComponentStore<Z> currentStoreZ;
    internal int componentIdT;
    internal int componentIdU;
    internal int componentIdV;
    internal int componentIdW;
    internal int componentIdX;
    internal int componentIdY;
    internal int componentIdZ;
    internal Entity currentEntity;

    internal QueryIterator(World world, QueryFilter filter)
    {
      componentIdT = ComponentRegistry.IdOf<T>();
      componentIdU = ComponentRegistry.IdOf<U>();
      componentIdV = ComponentRegistry.IdOf<V>();
      componentIdW = ComponentRegistry.IdOf<W>();
      componentIdX = ComponentRegistry.IdOf<X>();
      componentIdY = ComponentRegistry.IdOf<Y>();
      componentIdZ = ComponentRegistry.IdOf<Z>();
      state = new QueryIteratorState
      {
        world = world,
        filter = filter,
        componentIds = new int[] { componentIdT, componentIdU, componentIdV, componentIdW, componentIdX, componentIdY, componentIdZ },
        version = world.StructuralVersion,
        archetypeCount = world.ArchetypeStorage.ArchetypeCount,
        archetypeIndex = -1,
        chunkIndex = 0,
        row = 0,
        currentArchetype = null,
        currentChunkCount = 0,
      };
      currentStoreT = null;
      currentStoreU = null;
      currentStoreV = null;
      currentStoreW = null;
      currentStoreX = null;
      currentStoreY = null;
      currentStoreZ = null;
      currentEntity = default;
    }

    /// <summary> Duck-typed foreach support. </summary>
    public QueryIterator<T, U, V, W, X, Y, Z> GetEnumerator() => this;

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (state.MoveNext() == false)
        return false;
        
      var arch = state.currentArchetype;
      currentStoreT = (ComponentStore<T>)arch.GetStore(componentIdT);
      currentStoreU = (ComponentStore<U>)arch.GetStore(componentIdU);
      currentStoreV = (ComponentStore<V>)arch.GetStore(componentIdV);
      currentStoreW = (ComponentStore<W>)arch.GetStore(componentIdW);
      currentStoreX = (ComponentStore<X>)arch.GetStore(componentIdX);
      currentStoreY = (ComponentStore<Y>)arch.GetStore(componentIdY);
      currentStoreZ = (ComponentStore<Z>)arch.GetStore(componentIdZ);
      currentEntity = state.currentChunk.entities[state.row];

      return true;
    }

    /// <summary> Current query entry (valid after a successful <see cref="MoveNext"/>). </summary>
    public QueryEntry<T, U, V, W, X, Y, Z> Current => new QueryEntry<T, U, V, W, X, Y, Z>(currentEntity, currentStoreT, currentStoreU, currentStoreV, currentStoreW, currentStoreX, currentStoreY, currentStoreZ, state.chunkIndex, state.row);

    /// <summary> Number of live entities matching this query (full scan). </summary>
    public int Count => state.LiveCount();

    /// <summary> Number of archetypes matching this query (full scan). </summary>
    public int MatchingArchetypeCount => state.LiveMatchingArchetypeCount();

    /// <summary> Invoke <paramref name="fn"/> for each entity, passing a ref to component <typeparamref name="T"/>. </summary>
    public void ForEach(EntityComponentRefAction<T> fn)
    {
      while (MoveNext()) fn(currentEntity, ref currentStoreT.GetRef(state.chunkIndex, state.row));
    }
  }

  // ════════════════════════════════════════════════════════════════════════
  // 8 components
  // ════════════════════════════════════════════════════════════════════════

  public ref struct QueryEntry<T, U, V, W, X, Y, Z, A>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
    where X : unmanaged where Y : unmanaged where Z : unmanaged where A : unmanaged
  {
    /// <summary> Entity handle for the current row. </summary>
    public Entity entity;
    internal ComponentStore<T> storeT;
    internal ComponentStore<U> storeU;
    internal ComponentStore<V> storeV;
    internal ComponentStore<W> storeW;
    internal ComponentStore<X> storeX;
    internal ComponentStore<Y> storeY;
    internal ComponentStore<Z> storeZ;
    internal ComponentStore<A> storeA;
    internal int chunkIndex;
    internal int row;

    internal QueryEntry(Entity entity, ComponentStore<T> storeT, ComponentStore<U> storeU, ComponentStore<V> storeV, ComponentStore<W> storeW, ComponentStore<X> storeX, ComponentStore<Y> storeY, ComponentStore<Z> storeZ, ComponentStore<A> storeA, int chunkIndex, int row)
    {
      this.entity = entity;
      this.storeT = storeT;
      this.storeU = storeU;
      this.storeV = storeV;
      this.storeW = storeW;
      this.storeX = storeX;
      this.storeY = storeY;
      this.storeZ = storeZ;
      this.storeA = storeA;
      this.chunkIndex = chunkIndex;
      this.row = row;
    }

    /// <summary> Mutable reference to component <typeparamref name="T"/> for the current row. </summary>
    public ref T t => ref storeT.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="U"/> for the current row. </summary>
    public ref U u => ref storeU.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="V"/> for the current row. </summary>
    public ref V v => ref storeV.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="W"/> for the current row. </summary>
    public ref W w => ref storeW.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="X"/> for the current row. </summary>
    public ref X x => ref storeX.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="Y"/> for the current row. </summary>
    public ref Y y => ref storeY.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="Z"/> for the current row. </summary>
    public ref Z z => ref storeZ.GetRef(chunkIndex, row);

    /// <summary> Mutable reference to component <typeparamref name="A"/> for the current row. </summary>
    public ref A a => ref storeA.GetRef(chunkIndex, row);
  }

  public ref struct QueryIterator<T, U, V, W, X, Y, Z, A>
    where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
    where X : unmanaged where Y : unmanaged where Z : unmanaged where A : unmanaged
  {
    internal QueryIteratorState state;
    internal ComponentStore<T> currentStoreT;
    internal ComponentStore<U> currentStoreU;
    internal ComponentStore<V> currentStoreV;
    internal ComponentStore<W> currentStoreW;
    internal ComponentStore<X> currentStoreX;
    internal ComponentStore<Y> currentStoreY;
    internal ComponentStore<Z> currentStoreZ;
    internal ComponentStore<A> currentStoreA;
    internal int componentIdT;
    internal int componentIdU;
    internal int componentIdV;
    internal int componentIdW;
    internal int componentIdX;
    internal int componentIdY;
    internal int componentIdZ;
    internal int componentIdA;
    internal Entity currentEntity;

    internal QueryIterator(World world, QueryFilter filter)
    {
      componentIdT = ComponentRegistry.IdOf<T>();
      componentIdU = ComponentRegistry.IdOf<U>();
      componentIdV = ComponentRegistry.IdOf<V>();
      componentIdW = ComponentRegistry.IdOf<W>();
      componentIdX = ComponentRegistry.IdOf<X>();
      componentIdY = ComponentRegistry.IdOf<Y>();
      componentIdZ = ComponentRegistry.IdOf<Z>();
      componentIdA = ComponentRegistry.IdOf<A>();
      state = new QueryIteratorState
      {
        world = world,
        filter = filter,
        componentIds = new int[] { componentIdT, componentIdU, componentIdV, componentIdW, componentIdX, componentIdY, componentIdZ, componentIdA },
        version = world.StructuralVersion,
        archetypeCount = world.ArchetypeStorage.ArchetypeCount,
        archetypeIndex = -1,
        chunkIndex = 0,
        row = 0,
        currentArchetype = null,
        currentChunkCount = 0,
      };
      currentStoreT = null;
      currentStoreU = null;
      currentStoreV = null;
      currentStoreW = null;
      currentStoreX = null;
      currentStoreY = null;
      currentStoreZ = null;
      currentStoreA = null;
      currentEntity = default;
    }

    /// <summary> Duck-typed foreach support. </summary>
    public QueryIterator<T, U, V, W, X, Y, Z, A> GetEnumerator() => this;

    /// <summary> Advance to the next matching entity. </summary>
    public bool MoveNext()
    {
      if (state.MoveNext() == false)
        return false;
        
      var arch = state.currentArchetype;
      currentStoreT = (ComponentStore<T>)arch.GetStore(componentIdT);
      currentStoreU = (ComponentStore<U>)arch.GetStore(componentIdU);
      currentStoreV = (ComponentStore<V>)arch.GetStore(componentIdV);
      currentStoreW = (ComponentStore<W>)arch.GetStore(componentIdW);
      currentStoreX = (ComponentStore<X>)arch.GetStore(componentIdX);
      currentStoreY = (ComponentStore<Y>)arch.GetStore(componentIdY);
      currentStoreZ = (ComponentStore<Z>)arch.GetStore(componentIdZ);
      currentStoreA = (ComponentStore<A>)arch.GetStore(componentIdA);
      currentEntity = state.currentChunk.entities[state.row];

      return true;
    }

    /// <summary> Current query entry (valid after a successful <see cref="MoveNext"/>). </summary>
    public QueryEntry<T, U, V, W, X, Y, Z, A> Current => new QueryEntry<T, U, V, W, X, Y, Z, A>(currentEntity, currentStoreT, currentStoreU, currentStoreV, currentStoreW, currentStoreX, currentStoreY, currentStoreZ, currentStoreA, state.chunkIndex, state.row);

    /// <summary> Number of live entities matching this query (full scan). </summary>
    public int Count => state.LiveCount();

    /// <summary> Number of archetypes matching this query (full scan). </summary>
    public int MatchingArchetypeCount => state.LiveMatchingArchetypeCount();

    /// <summary> Invoke <paramref name="fn"/> for each entity, passing a ref to component <typeparamref name="T"/>. </summary>
    public void ForEach(EntityComponentRefAction<T> fn)
    {
      while (MoveNext()) fn(currentEntity, ref currentStoreT.GetRef(state.chunkIndex, state.row));
    }
  }
}
