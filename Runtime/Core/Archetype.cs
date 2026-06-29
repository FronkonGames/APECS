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
  /// <summary> A single archetype: a (mask, list of chunks, list of component stores) tuple. </summary>
  public sealed class Archetype : IDisposable
  {
    /// <summary> Stable archetype ID (starts at 1). </summary>
    public int Id { get; }

    /// <summary> Component membership mask for this archetype. </summary>
    public ComponentMask Mask { get; }

    /// <summary> One store per component type present in <see cref="Mask"/>. </summary>
    public List<IComponentStore> Stores { get; }

    /// <summary> Fixed-capacity entity blocks. </summary>
    public List<ArchetypeChunk> Chunks { get; }

    /// <summary> Total live entities across all chunks. </summary>
    public int EntityCount
    {
      /// <summary> Gets the live entity count. </summary>
      get;
      private set;
    }

    /// <summary> Number of allocated chunks. </summary>
    public int ChunkCount => Chunks.Count;

    /// <summary> componentId → index into <see cref="Stores"/>, or -1 if not present. </summary>
    private readonly int[] componentToStore;

    private bool disposed;

    public Archetype(int id, ComponentMask mask)
    {
      Id = id;
      Mask = mask;
      Stores = new List<IComponentStore>(4);
      Chunks = new List<ArchetypeChunk>(2);
      componentToStore = new int[ComponentMask.MaxTypes];
      
      for (int i = 0; i < componentToStore.Length; ++i)
        componentToStore[i] = -1;
    }

    /// <summary> Add a new component type store. </summary>
    public void AddStore(IComponentStore store)
    {
      if (store.ComponentId < 0 || store.ComponentId >= ComponentMask.MaxTypes)
        throw new ArgumentOutOfRangeException(nameof(store), "ComponentId out of range.");

      if (componentToStore[store.ComponentId] != -1)
        throw new InvalidOperationException($"[APECS] Archetype already has a store for component {store.ComponentId}.");

      componentToStore[store.ComponentId] = Stores.Count;
      Stores.Add(store);
    }

    /// <summary> True when this archetype includes the given component type. </summary>
    public bool HasComponent(int componentId)
    {
      if ((uint)componentId >= (uint)ComponentMask.MaxTypes)
        return false;

      return componentToStore[componentId] >= 0;
    }

    /// <summary> Return the store for <paramref name="componentId"/>, or <c>null</c> if absent. </summary>
    public IComponentStore GetStore(int componentId)
    {
      if ((uint)componentId >= (uint)ComponentMask.MaxTypes)
        return null;

      int idx = componentToStore[componentId];

      return idx >= 0 ? Stores[idx] : null;
    }

    /// <summary>
    /// Place the entity in the last chunk, growing (or allocating) a new chunk when the last is full.
    /// Returns the (chunk, row) where it landed.
    /// </summary>
    public (int chunk, int row) Add(Entity e)
    {
      ThrowIfDisposed();

      if (Chunks.Count == 0 || Chunks[^1].IsFull)
      {
        int newChunkIndex = Chunks.Count;
        var newChunk = ArchetypeChunk.Create();

        for (int i = 0; i < Stores.Count; ++i)
          Stores[i].GrowChunk(newChunkIndex);

        Chunks.Add(newChunk);
      }

      int lastChunkIndex = Chunks.Count - 1;
      var chunk = Chunks[lastChunkIndex];
      int row = chunk.count;
      chunk.entities[row] = e;
      chunk.count = row + 1;
      Chunks[lastChunkIndex] = chunk;

      EntityCount++;

      return (lastChunkIndex, row);
    }

    /// <summary>
    /// Place the entity in this archetype and copy every shared component from a source archetype.
    /// Components present in this archetype but absent in the source are left at the store's default
    /// value (the caller is expected to write them, e.g. the value being <c>AddComponent</c>'d).
    /// </summary>
    public (int chunk, int row) AddWithCopy(Entity e, Archetype source, int sourceChunk, int sourceRow)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));
      
      var (chunk, row) = Add(e);

      // Copy data for components present in both archetypes. We walk the source's component
      // set (typically the smaller one during migration) and ask its store to move the row's data into ours.
      for (int id = 0; id < ComponentMask.MaxTypes; ++id)
      {
        if (source.Mask.Has(id) == false)
          continue;

        if (Mask.Has(id) == false)
          continue;
          
        var srcStore = source.GetStore(id);
        var dstStore = GetStore(id);
        srcStore.MoveToChunk(dstStore, sourceChunk, sourceRow, chunk, row);
      }

      return (chunk, row);
    }

    /// <summary>
    /// Swap-back removal: the last entity in the chunk moves into the vacated row. Returns the swapped
    /// entity's <em>previous</em> location so the caller can update the SparseSet.
    /// </summary>
    public EntityRecord Remove(int chunkIndex, int row)
    {
      ThrowIfDisposed();

      if ((uint)chunkIndex >= (uint)Chunks.Count)
        throw new ArgumentOutOfRangeException(nameof(chunkIndex));

      var chunk = Chunks[chunkIndex];

      if ((uint)row >= (uint)chunk.count)
        throw new ArgumentOutOfRangeException(nameof(row));

      int swappedFromRow = chunk.count - 1;
      EntityRecord swappedPrevious;

      if (swappedFromRow != row)
      {
        for (int i = 0; i < Stores.Count; ++i)
          Stores[i].RemoveAt(chunkIndex, row, swappedFromRow);

        chunk.entities[row] = chunk.entities[swappedFromRow];
        swappedPrevious = new EntityRecord { ArchetypeId = Id, ChunkIndex = chunkIndex, Row = swappedFromRow };
      }
      else
      {
        // Removing the last element; no swap, but the caller still needs a placeholder record for the SparseSet.
        // The chunk/row values are meaningless because the entity is gone; the caller must not use this record
        // to update a live entity.
        swappedPrevious = new EntityRecord { ArchetypeId = Id, ChunkIndex = chunkIndex, Row = row };
      }

      chunk.count = swappedFromRow;
      Chunks[chunkIndex] = chunk;
      EntityCount--;

      return swappedPrevious;
    }

    public void Dispose()
    {
      if (disposed == true)
        return;

      disposed = true;

      for (int i = 0; i < Stores.Count; ++i)
        Stores[i].Dispose();

      Stores.Clear();

      for (int i = 0; i < Chunks.Count; ++i)
        Chunks[i].Dispose();

      Chunks.Clear();
      EntityCount = 0;
    }

    private void ThrowIfDisposed()
    {
      if (disposed == true)
        throw new ObjectDisposedException(nameof(Archetype));
    }
  }
}
