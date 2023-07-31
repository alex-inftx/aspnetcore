// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Antiforgery.Internal;

internal sealed partial class AntiforgeryMiddleware(IAntiforgery antiforgery, RequestDelegate next)
{
    private readonly RequestDelegate _next = next;
    private readonly IAntiforgery _antiforgery = antiforgery;

    private const string AntiforgeryMiddlewareWithEndpointInvokedKey = "__AntiforgeryMiddlewareWithEndpointInvoked";
    private static readonly object AntiforgeryMiddlewareWithEndpointInvokedValue = new object();

    public Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint is not null)
        {
            context.Items[AntiforgeryMiddlewareWithEndpointInvokedKey] = AntiforgeryMiddlewareWithEndpointInvokedValue;
        }

        var method = context.Request.Method;
        if (!HttpMethodExtensions.IsValidHttpMethodForForm(method))
        {
            return _next(context);
        }

        if (endpoint?.Metadata.GetMetadata<IAntiforgeryMetadata>() is { RequiresValidation: true })
        {
            return InvokeAwaited(context, endpoint);
        }

        return _next(context);
    }

    public async Task InvokeAwaited(HttpContext context, Endpoint endpoint)
    {
        try
        {
            SetRequestFormLimits(context, endpoint);
            await _antiforgery.ValidateRequestAsync(context);
            context.Features.Set(AntiforgeryValidationFeature.Valid);
        }
        catch (AntiforgeryValidationException e)
        {
            context.Features.Set<IAntiforgeryValidationFeature>(new AntiforgeryValidationFeature(false, e));
        }
        await _next(context);
    }

    private static void SetRequestFormLimits(HttpContext context, Endpoint endpoint)
    {
        var features = context.Features;
        var formFeature = features.Get<IFormFeature>();

        if (formFeature == null || formFeature?.Form == null)
        {
            // Request form has not been read yet, so set the limits
            var requestFormLimits = endpoint.Metadata.GetMetadata<IRequestFormLimitsMetadata>();
            if (requestFormLimits is null)
            {
                return;
            }
            var formOptions = new FormOptions
            {
                BufferBody = requestFormLimits.BufferBody,
                MemoryBufferThreshold = requestFormLimits.MemoryBufferThreshold,
                BufferBodyLengthLimit = requestFormLimits.BufferBodyLengthLimit,
                ValueCountLimit = requestFormLimits.ValueCountLimit,
                KeyLengthLimit = requestFormLimits.KeyLengthLimit,
                ValueLengthLimit = requestFormLimits.ValueLengthLimit,
                MultipartBoundaryLengthLimit = requestFormLimits.MultipartBoundaryLengthLimit,
                MultipartHeadersCountLimit = requestFormLimits.MultipartHeadersCountLimit,
                MultipartHeadersLengthLimit = requestFormLimits.MultipartHeadersLengthLimit,
                MultipartBodyLengthLimit = requestFormLimits.MultipartBodyLengthLimit
            };
            features.Set<IFormFeature>(new FormFeature(context.Request, formOptions));
            // Log.AppliedRequestFormLimits(_logger);
        }
        else
        {
            // Log.CannotApplyRequestFormLimits(_logger);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Warning, "Unable to apply configured form options since the request form has already been read.", EventName = "CannotApplyRequestFormLimits")]
        public static partial void CannotApplyRequestFormLimits(ILogger logger);

        [LoggerMessage(2, LogLevel.Debug, "Applied the configured form options on the current request.", EventName = "AppliedRequestFormLimits")]
        public static partial void AppliedRequestFormLimits(ILogger logger);
    }
}
