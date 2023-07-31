// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Http.Metadata;

/// <summary>
/// Interface marking attributes that specify limits associated with reading a form.
/// </summary>
public interface IRequestFormLimitsMetadata
{
    bool BufferBody { get; set; }
    int MemoryBufferThreshold { get; set; }
    long BufferBodyLengthLimit { get; set; }
    int ValueCountLimit { get; set; }
    int KeyLengthLimit { get; set; }
    int ValueLengthLimit { get; set; }
    int MultipartBoundaryLengthLimit { get; set; }
    int MultipartHeadersCountLimit { get; set; }
    int MultipartHeadersLengthLimit { get; set; }
    long MultipartBodyLengthLimit { get; set; }
}
