// This is free and unencumbered software released into the public domain.
// See the UNLICENSE file in the project root for more information.

using System;

namespace Neliva
{
    /// <summary>
    /// Encapsulates a method that fills a span with
    /// cryptographically strong random bytes.
    /// </summary>
    /// <param name="data">
    /// The span to fill with cryptographically strong random bytes.
    /// </param>
    /// <seealso cref="UidProvider"/>
    public delegate void UidRngFillAction(Span<byte> data);
}