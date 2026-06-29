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
  /// <summary> Stores service-style class resources and unmanaged value resources keyed by type. </summary>
  public sealed class ResourceContainer : IDisposable
  {
    private readonly Dictionary<Type, object> classResources = new();
    private readonly Dictionary<Type, object> valueResources = new();
    private bool disposed;

    /// <summary> Total number of registered class and value resources. </summary>
    public int Count => classResources.Count + valueResources.Count;

    // Class resources

    /// <summary> Register a reference-type resource keyed by <typeparamref name="T"/>. </summary>
    public void Add<T>(T resource) where T : class
    {
      ThrowIfDisposed();

      classResources[typeof(T)] = resource ?? throw new ArgumentNullException(nameof(resource));
    }

    /// <summary> Return the registered class resource, or <c>null</c> if absent. </summary>
    public T Get<T>() where T : class
    {
      if (classResources.TryGetValue(typeof(T), out var r) == true)
        return (T)r;
        
      return null;
    }

    /// <summary> True when a class resource of type <typeparamref name="T"/> is registered. </summary>
    public bool Has<T>() where T : class => classResources.ContainsKey(typeof(T));

    /// <summary> Remove the class resource of type <typeparamref name="T"/>. </summary>
    public void Remove<T>() where T : class => classResources.Remove(typeof(T));

    // Unmanaged value resources

    /// <summary> Register an unmanaged value resource keyed by <typeparamref name="T"/>. </summary>
    public void AddValue<T>(T value) where T : unmanaged
    {
      ThrowIfDisposed();
      // Box once; the caller is expected to use GetValue to retrieve as ref.
      valueResources[typeof(T)] = value;
    }

    /// <summary> Return a mutable reference to the registered value resource. </summary>
    public ref T GetValue<T>() where T : unmanaged
    {
      if (valueResources.TryGetValue(typeof(T), out var boxed) == false)
        throw new KeyNotFoundException($"[APECS] ResourceContainer: no value resource of type {typeof(T).Name}.");

      return ref System.Runtime.CompilerServices.Unsafe.Unbox<T>(boxed);
    }

    /// <summary> True when a value resource of type <typeparamref name="T"/> is registered. </summary>
    public bool HasValue<T>() where T : unmanaged => valueResources.ContainsKey(typeof(T));

    /// <summary> Remove the value resource of type <typeparamref name="T"/>. </summary>
    public void RemoveValue<T>() where T : unmanaged => valueResources.Remove(typeof(T));

    public void Dispose()
    {
      disposed = true;
      classResources.Clear();
      valueResources.Clear();
    }

    private void ThrowIfDisposed()
    {
      if (disposed == true)
        throw new ObjectDisposedException(nameof(ResourceContainer));
    }
  }
}
