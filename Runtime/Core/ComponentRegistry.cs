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
using System.Collections.Generic;

namespace FronkonGames.APECS
{
  /// <summary> Maps component types to stable, process-lifetime IDs (0..255). </summary>
  public static class ComponentRegistry
  {
    /// <summary> Maximum number of distinct component types. Alias of <see cref="ComponentMask.MaxTypes"/>. </summary>
    public const int MaxTypes = ComponentMask.MaxTypes;

    private static readonly Dictionary<Type, int> ids = new(64);
    private static readonly object sync = new();
    private static int count;

    /// <summary> Number of component types registered so far. </summary>
    public static int TypeCount
    {
      /// <summary> Gets the current registration count. </summary>
      get { lock (sync) return count; }
    }

    /// <summary> Return the stable ID for component type <typeparamref name="T"/>, registering it on first use. </summary>
    public static int IdOf<T>() where T : unmanaged => IdOf(typeof(T));

    /// <summary> Return the stable ID for <paramref name="type"/>, registering it on first use. </summary>
    public static int IdOf(Type type)
    {
      if (type == null) throw new ArgumentNullException(nameof(type));

      lock (sync)
      {
        if (ids.TryGetValue(type, out int existing) == true)
          return existing;

        if (count >= MaxTypes)
          throw new InvalidOperationException($"[APECS] ComponentRegistry: max {MaxTypes} component types reached, cannot register '{type.FullName}'.");

        int next = count++;
        ids[type] = next;
        return next;
      }
    }

    /// <summary> Reverse lookup: component ID → <see cref="Type"/>, or <c>null</c> if unknown. </summary>
    public static Type TypeOf(int id)
    {
      lock (sync)
      {
        foreach (var kv in ids)
          if (kv.Value == id)
            return kv.Key;
        return null;
      }
    }

    /// <summary> Short name for the component type at <paramref name="id"/>, or <c>"?"</c> if unknown. </summary>
    public static string NameOf(int id) => TypeOf(id)?.Name ?? "?";

    /// <summary> True when <typeparamref name="T"/> has been registered at least once. </summary>
    public static bool IsRegistered<T>() where T : unmanaged
    {
      lock (sync) return ids.ContainsKey(typeof(T));
    }
  }
}
