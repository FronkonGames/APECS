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
  /// <summary> Fixed-capacity block of entities inside an archetype. </summary>
  public struct ArchetypeChunk : IDisposable
  {
    /// <summary> Maximum entities per chunk. Tuned for L1/L2 cache locality. </summary>
    public const int Capacity = 128;

    /// <summary> Current occupancy (0..Capacity). Rows [0..Count) are valid. </summary>
    public int count;

    /// <summary> True when no further entity can be added without a new chunk. </summary>
    public readonly bool IsFull => count >= Capacity;

    /// <summary> Entities living in this chunk, dense-packed in [0..Count). Slot Count holds garbage. </summary>
    public NativeArray<Entity> entities;

    /// <summary> Allocate a new empty chunk with persistent entity storage. </summary>
    public static ArchetypeChunk Create()
    {
      return new ArchetypeChunk
      {
        count = 0,
        entities = new NativeArray<Entity>(Capacity, Allocator.Persistent, NativeArrayOptions.ClearMemory),
      };
    }

    public void Dispose()
    {
      if (entities.IsCreated == true)
        entities.Dispose();

      count = 0;
    }
  }
}
