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
namespace FronkonGames.APECS
{
  /// <summary> Per-frame sender side of an event channel. Returned by
  /// <see cref="SystemBase.GetEventWriter{T}"/>; lives on the stack only (ref struct). </summary>
  public readonly ref struct EventWriter<T> where T : unmanaged
  {
    readonly EventQueue<T> queue;

    internal EventWriter(EventQueue<T> queue) { this.queue = queue; }

    /// <summary> Events sent here are visible to readers in the next frame (after Swap). </summary>
    public void Send(T evt) => queue.Send(evt);

    /// <summary> Events sent here are visible in the same frame (bypasses the double buffer). </summary>
    public void SendImmediate(T evt) => queue.SendImmediate(evt);

    /// <summary> Number of events pending in the current write buffer. </summary>
    public int PendingCount => queue.PendingCount;
  }
}
