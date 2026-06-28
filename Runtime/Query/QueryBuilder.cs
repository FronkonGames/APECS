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
  /// <summary>
  /// Fluent query builder. Accumulate the required/excluded/any-of masks and call one of the <c>Build<T...>()</c>
  /// overloads to materialise an iterator. Value type to avoid allocations.
  /// </summary>
  public struct QueryBuilder
  {
    internal World world;
    internal ComponentMask required;
    internal ComponentMask excluded;
    internal ComponentMask anyOf;
    internal bool hasAnyOf;

    internal QueryBuilder(World world)
    {
      this.world = world;
      this.required = default;
      this.excluded = default;
      this.anyOf = default;
      this.hasAnyOf = false;
    }

    /// <summary> Require component <typeparamref name="T"/> in matching archetypes. </summary>
    public QueryBuilder With<T>() where T : unmanaged
    {
      required.Set(ComponentRegistry.IdOf<T>());

      return this;
    }

    /// <summary> Exclude entities that have component <typeparamref name="T"/>. </summary>
    public QueryBuilder Without<T>() where T : unmanaged
    {
      excluded.Set(ComponentRegistry.IdOf<T>());

      return this;
    }

    /// <summary> Require at least one of the listed component types. </summary>
    public QueryBuilder WithAny<T>() where T : unmanaged
    {
      anyOf.Set(ComponentRegistry.IdOf<T>());
      hasAnyOf = true;

      return this;
    }

    /// <summary> Require at least one of <typeparamref name="T"/> or <typeparamref name="U"/>. </summary>
    public QueryBuilder WithAny<T, U>() where T : unmanaged where U : unmanaged
    {
      anyOf.Set(ComponentRegistry.IdOf<T>());
      anyOf.Set(ComponentRegistry.IdOf<U>());
      hasAnyOf = true;

      return this;
    }

    /// <summary> Require at least one of three component types. </summary>
    public QueryBuilder WithAny<T, U, V>() where T : unmanaged where U : unmanaged where V : unmanaged
    {
      anyOf.Set(ComponentRegistry.IdOf<T>());
      anyOf.Set(ComponentRegistry.IdOf<U>());
      anyOf.Set(ComponentRegistry.IdOf<V>());
      hasAnyOf = true;

      return this;
    }

    internal QueryFilter BuildFilter() => new QueryFilter
    {
      required = required,
      excluded = excluded,
      anyOf = anyOf,
      hasAnyOf = hasAnyOf,
    };

    // Build<T...> overloads

    /// <summary> Materialise an iterator over entities with component <typeparamref name="T"/>. </summary>
    public QueryIterator<T> Build<T>() where T : unmanaged
    {
      var f = BuildFilter();
      f.required.Set(ComponentRegistry.IdOf<T>());

      return new QueryIterator<T>(world, f);
    }

    /// <summary> Materialise an iterator over entities with two required components. </summary>
    public QueryIterator<T, U> Build<T, U>() where T : unmanaged where U : unmanaged
    {
      var f = BuildFilter();
      f.required.Set(ComponentRegistry.IdOf<T>());
      f.required.Set(ComponentRegistry.IdOf<U>());

      return new QueryIterator<T, U>(world, f);
    }

    /// <summary> Materialise an iterator over entities with three required components. </summary>
    public QueryIterator<T, U, V> Build<T, U, V>() where T : unmanaged where U : unmanaged where V : unmanaged
    {
      var f = BuildFilter();
      f.required.Set(ComponentRegistry.IdOf<T>());
      f.required.Set(ComponentRegistry.IdOf<U>());
      f.required.Set(ComponentRegistry.IdOf<V>());

      return new QueryIterator<T, U, V>(world, f);
    }

    /// <summary> Materialise an iterator over entities with four required components. </summary>
    public QueryIterator<T, U, V, W> Build<T, U, V, W>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
    {
      var f = BuildFilter();
      f.required.Set(ComponentRegistry.IdOf<T>());
      f.required.Set(ComponentRegistry.IdOf<U>());
      f.required.Set(ComponentRegistry.IdOf<V>());
      f.required.Set(ComponentRegistry.IdOf<W>());

      return new QueryIterator<T, U, V, W>(world, f);
    }

    /// <summary> Materialise an iterator over entities with five required components. </summary>
    public QueryIterator<T, U, V, W, X> Build<T, U, V, W, X>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged where X : unmanaged
    {
      var f = BuildFilter();
      f.required.Set(ComponentRegistry.IdOf<T>());
      f.required.Set(ComponentRegistry.IdOf<U>());
      f.required.Set(ComponentRegistry.IdOf<V>());
      f.required.Set(ComponentRegistry.IdOf<W>());
      f.required.Set(ComponentRegistry.IdOf<X>());

      return new QueryIterator<T, U, V, W, X>(world, f);
    }

    /// <summary> Materialise an iterator over entities with six required components. </summary>
    public QueryIterator<T, U, V, W, X, Y> Build<T, U, V, W, X, Y>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
      where X : unmanaged where Y : unmanaged
    {
      var f = BuildFilter();
      f.required.Set(ComponentRegistry.IdOf<T>());
      f.required.Set(ComponentRegistry.IdOf<U>());
      f.required.Set(ComponentRegistry.IdOf<V>());
      f.required.Set(ComponentRegistry.IdOf<W>());
      f.required.Set(ComponentRegistry.IdOf<X>());
      f.required.Set(ComponentRegistry.IdOf<Y>());

      return new QueryIterator<T, U, V, W, X, Y>(world, f);
    }

    /// <summary> Materialise an iterator over entities with seven required components. </summary>
    public QueryIterator<T, U, V, W, X, Y, Z> Build<T, U, V, W, X, Y, Z>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
      where X : unmanaged where Y : unmanaged where Z : unmanaged
    {
      var f = BuildFilter();
      f.required.Set(ComponentRegistry.IdOf<T>());
      f.required.Set(ComponentRegistry.IdOf<U>());
      f.required.Set(ComponentRegistry.IdOf<V>());
      f.required.Set(ComponentRegistry.IdOf<W>());
      f.required.Set(ComponentRegistry.IdOf<X>());
      f.required.Set(ComponentRegistry.IdOf<Y>());
      f.required.Set(ComponentRegistry.IdOf<Z>());

      return new QueryIterator<T, U, V, W, X, Y, Z>(world, f);
    }

    /// <summary> Materialise an iterator over entities with eight required components. </summary>
    public QueryIterator<T, U, V, W, X, Y, Z, A> Build<T, U, V, W, X, Y, Z, A>()
      where T : unmanaged where U : unmanaged where V : unmanaged where W : unmanaged
      where X : unmanaged where Y : unmanaged where Z : unmanaged where A : unmanaged
    {
      var f = BuildFilter();
      f.required.Set(ComponentRegistry.IdOf<T>());
      f.required.Set(ComponentRegistry.IdOf<U>());
      f.required.Set(ComponentRegistry.IdOf<V>());
      f.required.Set(ComponentRegistry.IdOf<W>());
      f.required.Set(ComponentRegistry.IdOf<X>());
      f.required.Set(ComponentRegistry.IdOf<Y>());
      f.required.Set(ComponentRegistry.IdOf<Z>());
      f.required.Set(ComponentRegistry.IdOf<A>());

      return new QueryIterator<T, U, V, W, X, Y, Z, A>(world, f);
    }
  }
}
