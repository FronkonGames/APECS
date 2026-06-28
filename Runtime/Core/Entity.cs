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
using System.Runtime.InteropServices;

namespace FronkonGames.APECS
{
  /// <summary> Generational entity handle. Index 0 is reserved as the null entity. </summary>
  [StructLayout(LayoutKind.Sequential)]
  public readonly struct Entity : IEquatable<Entity>
  {
    /// <summary> Dense entity index (0 is reserved for <see cref="Null"/>). </summary>
    public readonly uint Index;

    /// <summary> Generation counter bumped on destroy to invalidate stale handles. </summary>
    public readonly uint Generation;

    /// <summary> True when this is the null entity (Index == 0 and Generation == 0). </summary>
    public bool IsNull => Index == 0u && Generation == 0u;

    /// <summary> Sentinel null entity handle (index and generation both zero). </summary>
    public static Entity Null => default;

    public Entity(uint index, uint generation)
    {
      Index = index;
      Generation = generation;
    }

    /// <inheritdoc />
    public bool Equals(Entity other) => Index == other.Index && Generation == other.Generation;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is Entity other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((int)(Index ^ (Generation * 0x9E3779B9u)));

    /// <inheritdoc />
    public override string ToString() => $"Entity(idx:{Index} gen:{Generation})";

    /// <summary> Compare two entity handles for equality. </summary>
    public static bool operator ==(Entity a, Entity b) => a.Equals(b);

    /// <summary> Compare two entity handles for inequality. </summary>
    public static bool operator !=(Entity a, Entity b) => !a.Equals(b);
  }
}
