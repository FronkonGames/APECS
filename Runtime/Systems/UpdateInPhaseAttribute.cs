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
  /// <summary> Marks which <see cref="Phase"/> the system runs in. </summary>
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
  public sealed class UpdateInPhaseAttribute : Attribute
  {
    /// <summary> Phase this system runs in. </summary>
    public Phase Phase { get; }

    public UpdateInPhaseAttribute(Phase phase) { Phase = phase; }
  }

  /// <summary> Marks that this system must run after the named type. </summary>
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
  public sealed class UpdateAfterAttribute : Attribute
  {
    /// <summary> System type that must run before this one. </summary>
    public Type SystemType { get; }

    public UpdateAfterAttribute(Type systemType) { SystemType = systemType; }
  }

  /// <summary> Marks that this system must run before the named type. </summary>
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
  public sealed class UpdateBeforeAttribute : Attribute
  {
    /// <summary> System type that must run after this one. </summary>
    public Type SystemType { get; }

    public UpdateBeforeAttribute(Type systemType) { SystemType = systemType; }
  }

  /// <summary> Marks a system as disabled by default. The scheduler still calls OnCreate,
  /// but does not tick it until <see cref="ISystem.Enabled"/> is set to true. </summary>
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
  public sealed class DisableByDefaultAttribute : Attribute { }
}
