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
  /// <summary> 256-bit component membership mask (4 x 64-bit words). Pure unmanaged, Burst-friendly. </summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct ComponentMask : IEquatable<ComponentMask>
  {
    /// <summary> Number of 64-bit words in the mask. </summary>
    public const int WordCount = 4;

    /// <summary> Maximum number of distinct component types (WordCount × 64 bits). </summary>
    public const int MaxTypes = WordCount * 64;

    /// <summary> Mask array. </summary>
    public unsafe fixed ulong Mask[WordCount];

    /// <summary> True when the bit for component <paramref name="id"/> is set. </summary>
    public bool Has(int id)
    {
      if ((uint)id >= (uint)MaxTypes)
        throw new ArgumentOutOfRangeException(nameof(id), id, $"id must be in [0, {MaxTypes}).");

      int word = id >> 6;
      int bit = id & 63;
      unsafe { return (Mask[word] & (1UL << bit)) != 0UL; }
    }

    /// <summary> Set the bit for component <paramref name="id"/>. </summary>
    public void Set(int id)
    {
      if ((uint)id >= (uint)MaxTypes)
        throw new ArgumentOutOfRangeException(nameof(id), id, $"id must be in [0, {MaxTypes}).");

      int word = id >> 6;
      int bit = id & 63;
      unsafe { Mask[word] |= (1UL << bit); }
    }

    /// <summary> Clear the bit for component <paramref name="id"/>. </summary>
    public void Unset(int id)
    {
      if ((uint)id >= (uint)MaxTypes)
        throw new ArgumentOutOfRangeException(nameof(id), id, $"id must be in [0, {MaxTypes}).");

      int word = id >> 6;
      int bit = id & 63;
      unsafe { Mask[word] &= ~(1UL << bit); }
    }

    /// <summary> True when every bit set in <paramref name="other"/> is also set in this mask. </summary>
    public bool HasAll(ComponentMask other)
    {
      unsafe
      {
        return (Mask[0] & other.Mask[0]) == other.Mask[0]
            && (Mask[1] & other.Mask[1]) == other.Mask[1]
            && (Mask[2] & other.Mask[2]) == other.Mask[2]
            && (Mask[3] & other.Mask[3]) == other.Mask[3];
      }
    }

    /// <summary> True when at least one bit set in <paramref name="other"/> is also set in this mask. </summary>
    public bool HasAny(ComponentMask other)
    {
      unsafe
      {
        return (Mask[0] & other.Mask[0]) != 0UL
            || (Mask[1] & other.Mask[1]) != 0UL
            || (Mask[2] & other.Mask[2]) != 0UL
            || (Mask[3] & other.Mask[3]) != 0UL;
      }
    }

    /// <summary> True when no bit set in <paramref name="other"/> is set in this mask. </summary>
    public bool HasNone(ComponentMask other)
    {
      unsafe
      {
        return (Mask[0] & other.Mask[0]) == 0UL
            && (Mask[1] & other.Mask[1]) == 0UL
            && (Mask[2] & other.Mask[2]) == 0UL
            && (Mask[3] & other.Mask[3]) == 0UL;
      }
    }

    /// <summary> Build a mask containing only component <typeparamref name="T"/>. </summary>
    public static ComponentMask Of<T>() where T : unmanaged
    {
      ComponentMask m = default;
      m.Set(ComponentRegistry.IdOf<T>());

      return m;
    }

    /// <summary> Build a mask containing components <typeparamref name="T"/> and <typeparamref name="U"/>. </summary>
    public static ComponentMask Of<T, U>() where T : unmanaged where U : unmanaged
    {
      ComponentMask m = Of<T>();
      m.Set(ComponentRegistry.IdOf<U>());

      return m;
    }

    /// <summary> Build a mask containing components <typeparamref name="T"/>, <typeparamref name="U"/> and <typeparamref name="V"/>. </summary>
    public static ComponentMask Of<T, U, V>() where T : unmanaged where U : unmanaged where V : unmanaged
    {
      ComponentMask m = Of<T, U>();
      m.Set(ComponentRegistry.IdOf<V>());

      return m;
    }

    /// <summary> Build a mask containing four component types. </summary>
    public static ComponentMask Of<T, U, V, W>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
    {
      ComponentMask m = Of<T, U, V>();
      m.Set(ComponentRegistry.IdOf<W>());

      return m;
    }

    /// <summary> Build a mask containing five component types. </summary>
    public static ComponentMask Of<T, U, V, W, X>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged where X : unmanaged
    {
      ComponentMask m = Of<T, U, V, W>();
      m.Set(ComponentRegistry.IdOf<X>());

      return m;
    }

    /// <summary> Build a mask containing six component types. </summary>
    public static ComponentMask Of<T, U, V, W, X, Y>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
      where X : unmanaged where Y : unmanaged
    {
      ComponentMask m = Of<T, U, V, W, X>();
      m.Set(ComponentRegistry.IdOf<Y>());

      return m;
    }

    /// <summary> Build a mask containing seven component types. </summary>
    public static ComponentMask Of<T, U, V, W, X, Y, Z>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
      where X : unmanaged where Y : unmanaged where Z : unmanaged
    {
      ComponentMask m = Of<T, U, V, W, X, Y>();
      m.Set(ComponentRegistry.IdOf<Z>());

      return m;
    }

    /// <summary> Build a mask containing eight component types. </summary>
    public static ComponentMask Of<T, U, V, W, X, Y, Z, A>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
      where X : unmanaged where Y : unmanaged where Z : unmanaged where A : unmanaged
    {
      ComponentMask m = Of<T, U, V, W, X, Y, Z>();
      m.Set(ComponentRegistry.IdOf<A>());

      return m;
    }

    /// <inheritdoc />
    public bool Equals(ComponentMask other)
    {
      unsafe
      {
        return Mask[0] == other.Mask[0]
            && Mask[1] == other.Mask[1]
            && Mask[2] == other.Mask[2]
            && Mask[3] == other.Mask[3];
      }
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is ComponentMask other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
      unsafe
      {
        unchecked
        {
          int h = (int)(Mask[0] ^ (Mask[0] >> 32));
          h = (h * 397) ^ (int)(Mask[1] ^ (Mask[1] >> 32));
          h = (h * 397) ^ (int)(Mask[2] ^ (Mask[2] >> 32));
          h = (h * 397) ^ (int)(Mask[3] ^ (Mask[3] >> 32));

          return h;
        }
      }
    }

    /// <summary> Compare two masks for equality. </summary>
    public static bool operator ==(ComponentMask a, ComponentMask b) => a.Equals(b);

    /// <summary> Compare two masks for inequality. </summary>
    public static bool operator !=(ComponentMask a, ComponentMask b) => !a.Equals(b);
  }
}
