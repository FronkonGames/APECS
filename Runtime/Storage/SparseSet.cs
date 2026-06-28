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
  /// <summary> A live entity's position inside its archetype. </summary>
  public struct EntityRecord
  {
    /// <summary> Archetype that owns this entity. </summary>
    public int ArchetypeId;

    /// <summary> Index into the archetype's chunk list. </summary>
    public int ChunkIndex;

    /// <summary> Dense row within the chunk. </summary>
    public int Row;
  }

  /// <summary>
  /// Generational entity index → location map.
  /// - Liveness API (Create / Destroy / IsAlive / Count) tracks which indices are in use.
  /// - Location API (Set / TryGet / Get / Remove) tracks the archetype/chunk/row of each
  ///   live entity, written by World when adding to an archetype and read on every component access.
  /// </summary>
  public sealed class SparseSet : IDisposable
  {
    const int InitialCapacity = 64;

    private uint[] generations;
    private EntityRecord[] locations;
    private Stack<int> freeIndices;
    private int nextIndex;
    private int aliveCount;
    private bool disposed;

    /// <summary> Number of live entities tracked. </summary>
    public int Count => aliveCount;

    /// <summary> Current capacity of the index/generation arrays. </summary>
    public int Capacity => generations?.Length ?? 0;

    public SparseSet() : this(InitialCapacity) { }

    public SparseSet(int initialCapacity)
    {
      if (initialCapacity < 2)
        initialCapacity = 2;

      generations = new uint[initialCapacity];
      locations = new EntityRecord[initialCapacity];
      freeIndices = new Stack<int>(initialCapacity);
      nextIndex = 1; // index 0 is reserved for Entity.Null
      aliveCount = 0;
    }

    // Liveness API

    /// <summary> Allocate a new entity. Reuses a freed index when available. </summary>
    public Entity Create()
    {
      ThrowIfDisposed();

      int index;
      if (freeIndices.Count > 0)
        index = freeIndices.Pop();
      else
      {
        if (nextIndex >= generations.Length)
        {
          Array.Resize(ref generations, generations.Length * 2);
          Array.Resize(ref locations, locations.Length * 2);
        }
        index = nextIndex++;
      }
      unchecked { generations[index]++; }
      aliveCount++;
      
      return new Entity((uint)index, generations[index]);
    }

    /// <summary> Mark entity dead, bump its generation, clear its location, return the index to the free list. </summary>
    public void Destroy(Entity e)
    {
      ThrowIfDisposed();

      if (e.Index == 0u || e.Index >= (uint)generations.Length)
        return;
      
      if (generations[e.Index] != e.Generation)
        return; // stale handle, no-op

      unchecked { generations[e.Index]++; }
      locations[e.Index] = default;
      freeIndices.Push((int)e.Index);
      aliveCount--;
    }

    /// <summary> True iff the index is in use AND the generation matches. </summary>
    public bool IsAlive(Entity e)
    {
      if (disposed == true)
        return false;
        
      if (e.Index == 0u)
        return false;

      if (e.Index >= (uint)generations.Length)
        return false;

      return generations[e.Index] == e.Generation;
    }

    // Location API

    /// <summary> Store the entity's archetype / chunk / row. No-op for stale or invalid handles. </summary>
    public void Set(Entity e, EntityRecord record)
    {
      ThrowIfDisposed();

      if (e.Index == 0u || e.Index >= (uint)generations.Length)
        return;

      if (generations[e.Index] != e.Generation)
        return; // stale, no-op

      locations[e.Index] = record;
    }

    /// <summary> True iff the entity is alive and has a stored location. </summary>
    public bool TryGet(Entity e, out EntityRecord record)
    {
      if (disposed == true)
      {
        record = default;
        return false;
      }

      if (e.Index == 0u || e.Index >= (uint)generations.Length)
      {
        record = default;
        return false;
      }

      if (generations[e.Index] != e.Generation)
      {
        record = default;
        return false;
      }

      record = locations[e.Index];
      return true;
    }

    /// <summary> Returns the entity's stored location. Throws if missing or stale. </summary>
    public EntityRecord Get(Entity e)
    {
      if (TryGet(e, out var r) == false)
        throw new KeyNotFoundException($"[APECS] SparseSet: no location for {e}.");
      
      return r;
    }

    /// <summary> Clear the entity's stored location without destroying it. No-op for stale handles. </summary>
    public void Remove(Entity e)
    {
      ThrowIfDisposed();

      if (e.Index == 0u || e.Index >= (uint)generations.Length)
        return;

      if (generations[e.Index] != e.Generation)
        return;

      locations[e.Index] = default;
    }

    // Lifecycle

    public void Dispose()
    {
      disposed = true;
      generations = null;
      locations = null;
      freeIndices = null;
    }

    private void ThrowIfDisposed()
    {
      if (disposed == true)
        throw new ObjectDisposedException(nameof(SparseSet));
    }
  }
}
