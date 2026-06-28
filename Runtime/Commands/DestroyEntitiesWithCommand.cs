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
  internal sealed class DestroyEntitiesWithCommand : ICommand
  {
    public int componentId;

    public DestroyEntitiesWithCommand(int componentId) { this.componentId = componentId; }

    public void Execute(CommandBuffer buffer, World world)
    {
      var toDestroy = new List<Entity>();
      int n = world.ArchetypeStorage.ArchetypeCount;
      for (int a = 0; a < n; ++a)
      {
        var arch = world.ArchetypeStorage.All[a];
        if (arch.HasComponent(componentId) == false)
          continue;

        for (int c = 0; c < arch.ChunkCount; ++c)
        {
          var chunk = arch.Chunks[c];
          for (int r = 0; r < chunk.count; ++r)
            toDestroy.Add(chunk.entities[r]);
        }
      }

      for (int i = 0; i < toDestroy.Count; ++i)
      {
        if (world.IsAlive(toDestroy[i]) == true)
          world.DestroyEntity(toDestroy[i]);
      }
    }
  }
}
