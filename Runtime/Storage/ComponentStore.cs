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
  /// <summary>
  /// Per-component-type chunked storage. The backing <see cref="NativeList{T}"/> is laid out so that chunk I, row J lives
  /// at index <c>I * ArchetypeChunk.Capacity + J</c>. The chunk's own <see cref="ArchetypeChunk.count"/> tracks which
  /// slots in the chunk's reserved range are currently live.
  /// </summary>
  public sealed class ComponentStore<T> : IComponentStore where T : unmanaged
  {
    /// <inheritdoc />
    public int ComponentId { get; }

    private NativeList<T> data;

    public ComponentStore(int componentId)
    {
      ComponentId = componentId;
      data = new NativeList<T>(ArchetypeChunk.Capacity, Allocator.Persistent);
    }

    public void Dispose()
    {
      if (data.IsCreated == true)
        data.Dispose();
    }

    private int FlatIndex(int chunkIndex, int row) => chunkIndex * ArchetypeChunk.Capacity + row;

    /// <summary> Slice of the backing store covering a full chunk (live rows in [0..Count)). </summary>
    public NativeArray<T> GetChunkArray(int chunkIndex)
    {
      int start = chunkIndex * ArchetypeChunk.Capacity;

      return data.AsArray().GetSubArray(start, ArchetypeChunk.Capacity);
    }

    /// <summary> Slice of the backing store covering the first <paramref name="count"/> rows of a chunk. </summary>
    public NativeArray<T> GetChunkArray(int chunkIndex, int count)
    {
      int start = chunkIndex * ArchetypeChunk.Capacity;

      return data.AsArray().GetSubArray(start, count);
    }

    /// <summary> Return a mutable reference to the element at (<paramref name="chunkIndex"/>, <paramref name="row"/>). </summary>
    public ref T GetRef(int chunkIndex, int row)
    {
      int i = FlatIndex(chunkIndex, row);
      if ((uint)i >= (uint)data.Length)
        throw new ArgumentOutOfRangeException($"[APECS] ComponentStore<{typeof(T).Name}>: ({chunkIndex},{row}) out of range.");

      return ref data.ElementAt(i);
    }

    /// <summary> Read the element at (<paramref name="chunkIndex"/>, <paramref name="row"/>). </summary>
    public T Get(int chunkIndex, int row)
    {
      int i = FlatIndex(chunkIndex, row);
      if ((uint)i >= (uint)data.Length)
        throw new ArgumentOutOfRangeException($"[APECS] ComponentStore<{typeof(T).Name}>: ({chunkIndex},{row}) out of range.");

      return data[i];
    }

    /// <summary> Write <paramref name="value"/> at (<paramref name="chunkIndex"/>, <paramref name="row"/>). </summary>
    public void Set(int chunkIndex, int row, T value)
    {
      int i = FlatIndex(chunkIndex, row);
      if ((uint)i >= (uint)data.Length)
        throw new ArgumentOutOfRangeException($"[APECS] ComponentStore<{typeof(T).Name}>: ({chunkIndex},{row}) out of range.");

      data[i] = value;
    }

    /// <inheritdoc />
    public void GrowChunk(int chunkIndex)
    {
      int required = (chunkIndex + 1) * ArchetypeChunk.Capacity;
      if (data.Length < required)
        data.Resize(required, NativeArrayOptions.ClearMemory);
    }

    /// <inheritdoc />
    public void RemoveAt(int chunkIndex, int row, int lastRowInChunk)
    {
      int i = FlatIndex(chunkIndex, row);
      int last = FlatIndex(chunkIndex, lastRowInChunk);
      
      if (i != last)
        data[i] = data[last];
      // data[last] is left in place; the chunk's Count decrement makes it logically free.
    }

    /// <inheritdoc />
    public void MoveToChunk(IComponentStore dest, int srcChunk, int srcRow, int dstChunk, int dstRow)
    {
      var typed = dest as ComponentStore<T>;
      if (typed == null)
        throw new ArgumentException($"[APECS] ComponentStore<{typeof(T).Name}>: destination is a different component type.", nameof(dest));

      int srcI = FlatIndex(srcChunk, srcRow);
      int dstI = typed.FlatIndex(dstChunk, dstRow);
      typed.data[dstI] = data[srcI];
    }
  }
}
