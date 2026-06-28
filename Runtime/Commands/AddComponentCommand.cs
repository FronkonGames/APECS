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
  internal sealed class AddComponentCommand : ICommand
  {
    public Entity entity;
    public Type valueType;
    public object value;

    public AddComponentCommand(Entity entity, Type valueType, object value)
    {
      this.entity = entity;
      this.valueType = valueType;
      this.value = value;
    }

    public void Execute(CommandBuffer buffer, World world)
    {
      Entity real = buffer.Resolve(entity);
      var method = typeof(World).GetMethod(nameof(World.AddComponent)).MakeGenericMethod(valueType);

      try
      {
        method.Invoke(world, new object[] { real, value });
      }
      catch (System.Reflection.TargetInvocationException tie)
      {
        throw tie.InnerException;
      }
    }
  }
}
