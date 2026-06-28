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
using System.Collections.Generic;

namespace FronkonGames.APECS
{
  /// <summary>
  /// Single per-world system that owns every per-system <see cref="CommandBuffer"/>. When it ticks, it plays every
  /// buffer back into the world and clears them, the spec's "end-of-phase flush". The bootstrap registers one
  /// in <see cref="Phase.PostUpdate"/>; other phases can be added by registering additional instances.
  /// </summary>
  [UpdateInPhase(Phase.PostUpdate)]
  public class CommandBufferSystem : SystemBase
  {
    private readonly Dictionary<ISystem, CommandBuffer> buffers = new();

    /// <summary> Returns (lazily creating) the buffer owned by <paramref name="owner"/>. </summary>
    public CommandBuffer GetBuffer(ISystem owner)
    {
      if (owner == null)
        throw new System.ArgumentNullException(nameof(owner));

      if (buffers.TryGetValue(owner, out var buffer) == false)
      {
        buffer = new CommandBuffer(World);
        buffers[owner] = buffer;
      }

      return buffer;
    }

    /// <summary> All registered buffers. Useful for inspection; do not modify. </summary>
    public IReadOnlyDictionary<ISystem, CommandBuffer> Buffers => buffers;

    protected override void Update()
    {
      // Play back every recorded buffer. We snapshot the keys to avoid mutation
      // issues if a Playback somehow causes a new buffer to be registered.
      if (buffers.Count == 0)
        return;
      
      var keys = new List<ISystem>(buffers.Keys);

      for (int i = 0; i < keys.Count; ++i)
      {
        var b = buffers[keys[i]];
        b.Playback(World);
        b.Clear();
      }
    }
  }
}
