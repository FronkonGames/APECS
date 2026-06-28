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
using Unity.Collections;

namespace FronkonGames.APECS
{
  /// <summary> Per-frame receiver side of an event channel. Returned by
  /// <see cref="SystemBase.GetEventReader{T}"/>; lives on the stack only (ref struct). </summary>
  public ref struct EventReader<T> where T : unmanaged
  {
    private readonly EventQueue<T> queue;
    private int cursor;

    public EventReader(EventQueue<T> queue)
    {
      this.queue = queue;
      this.cursor = 0;
    }

    /// <summary> True if no events remain to be read in this frame. </summary>
    public readonly bool IsEmpty => cursor >= queue.ReadableCount;

    /// <summary> Remaining events to be read (total - already-consumed cursor). </summary>
    public int Count
    {
      get
      {
        int total = queue.ReadableCount;
        return total > cursor ? total - cursor : 0;
      }
    }

    /// <summary> The full readable buffer for job consumption. The reader's own cursor is
    /// independent of this array — iterating with foreach and indexing into AsNativeArray
    /// can be done side by side. </summary>
    public readonly NativeArray<T> AsNativeArray() => queue.GetReadableEvents();

    /// <summary> Duck-typed foreach support: <c>foreach (var evt in reader) { ... }</c>. </summary>
    public readonly EventReader<T> GetEnumerator() => this;

    /// <summary> Advance the read cursor; returns false when all events have been consumed. </summary>
    public bool MoveNext()
    {
      if (cursor < queue.ReadableCount)
      {
        cursor++;
        return true;
      }
      return false;
    }

    /// <summary> Value copy of the current event (events are immutable once sent). </summary>
    public readonly T Current => queue.GetReadableEvents()[cursor - 1];
  }
}
