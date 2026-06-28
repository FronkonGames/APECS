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
  /// Non-generic handle for a double-buffered event queue. Stored in the World's <c>Dictionary<Type, IEventQueue></c>
  /// so the scheduler can call <see cref="Swap"/> on every queue at once without knowing the type parameter.
  /// </summary>
  public interface IEventQueue
  {
    /// <summary> Flip the write/read buffers. Events sent last phase are now readable; the new write buffer is cleared. </summary>
    void Swap();

    /// <summary> Clear both event buffers. </summary>
    void Clear();

    /// <summary> Release native memory held by the queue. </summary>
    void Dispose();
  }

  /// <summary>
  /// Per-event-type double buffer. Writers fill one side, readers drain the other. The scheduler swaps the two
  /// at the start of each <see cref="Phase.Update"/>, <see cref="Phase.FixedUpdate"/> and <see cref="Phase.LateUpdate"/>
  /// tick, events are strictly transient.
  /// </summary>
  public sealed class EventQueue<T> : IEventQueue, IDisposable where T : unmanaged
  {
    private NativeList<T> bufferA;
    private NativeList<T> bufferB;
    private bool writeIsA = true;
    private bool disposed;

    /// <summary> Number of events in the current write buffer. </summary>
    public int PendingCount  => (writeIsA ? bufferA : bufferB).Length;

    /// <summary> Number of events available to read this frame. </summary>
    public int ReadableCount => (writeIsA ? bufferB : bufferA).Length;

    public EventQueue()
    {
      bufferA = new NativeList<T>(16, Allocator.Persistent);
      bufferB = new NativeList<T>(16, Allocator.Persistent);
    }

    /// <summary> Append an event to the write buffer (visible after the next <see cref="Swap"/>). </summary>
    public void Send(T evt)
    {
      ThrowIfDisposed();

      var write = writeIsA ? bufferA : bufferB;
      write.Add(evt);
    }

    /// <summary> Add directly to the readable buffer — visible in the same frame, no
    /// <see cref="Swap"/> required. </summary>
    public void SendImmediate(T evt)
    {
      ThrowIfDisposed();

      var read = writeIsA ? bufferB : bufferA;
      read.Add(evt);
    }

    /// <summary> Flip write/read buffers and clear the new write side. </summary>
    public void Swap()
    {
      if (disposed == true)
        return;

      writeIsA = !writeIsA;

      // The new write buffer is what was the read buffer; clear it before the next send.
      (writeIsA ? bufferA : bufferB).Clear();
    }

    /// <summary> Clear both buffers without swapping. </summary>
    public void Clear()
    {
      if (disposed == true)
        return;

      bufferA.Clear();
      bufferB.Clear();
    }

    /// <summary> Native view of events readable this frame. </summary>
    public NativeArray<T> GetReadableEvents()
    {
      ThrowIfDisposed();

      var read = writeIsA ? bufferB : bufferA;
      return read.AsArray();
    }

    public void Dispose()
    {
      if (disposed == true)
        return;

      if (bufferA.IsCreated == true)
        bufferA.Dispose();

      if (bufferB.IsCreated == true)
        bufferB.Dispose();
    }

    private void ThrowIfDisposed()
    {
      if (disposed == true)
        throw new ObjectDisposedException(nameof(EventQueue<T>));
    }
  }
}
