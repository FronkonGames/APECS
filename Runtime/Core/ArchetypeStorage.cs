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
  /// Owns all archetypes in a world. <see cref="GetOrCreate"/> returns the existing archetype for a mask,
  /// or materializes a new one (lazily populating its stores via the store-factory delegate supplied at
  /// construction time).
  /// </summary>
  public sealed class ArchetypeStorage : IDisposable
  {
    /// <summary> Number of distinct archetypes materialised in this storage. </summary>
    public int ArchetypeCount => archetypesById.Count;

    /// <summary> All archetypes, indexed by <see cref="Archetype.Id"/> minus one. </summary>
    public IReadOnlyList<Archetype> All => archetypesById;

    private readonly Dictionary<ComponentMask, Archetype> archetypesByMask = new();

    private readonly List<Archetype> archetypesById = new();

    private readonly Func<int, IComponentStore> storeFactory;

    private int nextId = 1;

    private bool disposed;

    public ArchetypeStorage(Func<int, IComponentStore> storeFactory)
    {
      this.storeFactory = storeFactory ?? throw new ArgumentNullException(nameof(storeFactory));
    }

    /// <summary> Returns the existing archetype for the given mask, or creates a new one. </summary>
    public Archetype GetOrCreate(ComponentMask mask)
    {
      ThrowIfDisposed();

      if (archetypesByMask.TryGetValue(mask, out var existing) == true)
        return existing;

      var arch = new Archetype(nextId++, mask);

      for (int id = 0; id < ComponentMask.MaxTypes; ++id)
      {
        if (mask.Has(id) == false)
          continue;

        var store = storeFactory(id) ?? throw new InvalidOperationException(
            $"ArchetypeStorage.GetOrCreate: no store factory for component ID {id}. " +
            "Call world.RegisterStoreFactory<T>() before creating entities with that component.");
        arch.AddStore(store);
      }

      archetypesByMask[mask] = arch;
      archetypesById.Add(arch);

      return arch;
    }

    /// <summary> Look up an archetype by its stable ID, or <c>null</c> if not found. </summary>
    public Archetype GetById(int id)
    {
      // ids start at 1; list is 0-indexed
      int idx = id - 1;
      if (idx < 0 || idx >= archetypesById.Count)
        return null;

      return archetypesById[idx];
    }

    public void Dispose()
    {
      if (disposed == true)
        return;

      disposed = true;

      for (int i = 0; i < archetypesById.Count; ++i)
        archetypesById[i].Dispose();

      archetypesById.Clear();
      archetypesByMask.Clear();
    }

    private void ThrowIfDisposed()
    {
      if (disposed == true)
        throw new ObjectDisposedException(nameof(ArchetypeStorage));
    }
  }
}
