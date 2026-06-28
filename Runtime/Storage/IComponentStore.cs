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

namespace FronkonGames.APECS
{
  /// <summary>
  /// Chunked per-component-type storage inside an archetype. One store exists per distinct component type in the archetype's mask. </summary>
  public interface IComponentStore : IDisposable
  {
    /// <summary> Stable component type ID assigned by <see cref="ComponentRegistry"/>. </summary>
    int ComponentId { get; }

    /// <summary> Ensure the backing store has room for the given chunk's <see cref="ArchetypeChunk.Capacity"/> slots. </summary>
    void GrowChunk(int chunkIndex);

    /// <summary> Swap-back removal: row receives the last *live* row of the chunk; chunk Count is expected to drop by one. </summary>
    void RemoveAt(int chunkIndex, int row, int lastRowInChunk);

    /// <summary> Copy one element from this store to another at a different chunk/row. </summary>
    void MoveToChunk(IComponentStore dest, int srcChunk, int srcRow, int dstChunk, int dstRow);
  }
}
