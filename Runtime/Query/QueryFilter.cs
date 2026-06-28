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
  /// <summary> Pure value struct describing which archetypes a query matches. </summary>
  public struct QueryFilter
  {
    /// <summary> All archetypes in this set must be present. </summary>
    public ComponentMask required;

    /// <summary> None of these components may be present. </summary>
    public ComponentMask excluded;

    /// <summary> At least one of these must be present (ignored when <see cref="hasAnyOf"/> is false). </summary>
    public ComponentMask anyOf;

    /// <summary> True when <see cref="anyOf"/> should be applied; false treats AnyOf as empty. </summary>
    public bool hasAnyOf;

    /// <summary> True when <paramref name="archetypeMask"/> satisfies this filter. </summary>
    public readonly bool Matches(ComponentMask archetypeMask)
    {
      if (archetypeMask.HasAll(required) == false)
        return false;

      if (archetypeMask.HasNone(excluded) == false)
        return false;
        
      if (hasAnyOf == true && archetypeMask.HasAny(anyOf) == false)
        return false;

      return true;
    }
  }
}
