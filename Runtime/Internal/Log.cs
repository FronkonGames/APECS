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
using UnityEngine;

namespace FronkonGames.APECS
{
  /// <summary> Log utils. </summary>
  internal static class Log
  {
    /// <summary> Info message. </summary>
    /// <param name="message">Message.</param>
    public static void Info(string message) => Debug.Log($"[{Constants.Asset.AssemblyName}] {message}.");

    /// <summary> Warning message. </summary>
    /// <param name="message">Message.</param>
    public static void Warning(string message) => Debug.LogWarning($"[{Constants.Asset.AssemblyName}] {message}.");

    /// <summary> Error message. </summary>
    /// <param name="message">Message.</param>
    public static void Error(string message) => Debug.LogError($"[{Constants.Asset.AssemblyName}] {message} Please contact with '{Constants.Support.Email}' and send the log file.");
  }
}