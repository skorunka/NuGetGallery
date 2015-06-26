// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Web.Http;
using System.Web.Http.OData.Formatter;
using System.Web.Http.OData.Formatter.Deserialization;
using NuGetGallery.OData.Serializers;

namespace NuGetGallery
{
    public static class NuGetODataConfig
    {
        public const int SearchCacheTime = 30;

        public static void Register(HttpConfiguration config)
        {
            // Add OData formatters (application/atom+xml)
            var odataFormatters = ODataMediaTypeFormatters.Create(
                new CustomSerializerProvider(provider => new NuGetEntityTypeSerializer(provider)),
                new DefaultODataDeserializerProvider());

            // Disable json and atomsvc - if these are ever needed, please reorder them so they are at the end of the collection. This will save you a few hours of debugging.
            var reorderedFormatters = odataFormatters.Where(f => !f.SupportedMediaTypes.Any(m => m.MediaType == "application/atomsvc+xml" || m.MediaType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)));

            config.Formatters.Clear();
            config.Formatters.InsertRange(0, reorderedFormatters);
            
            // Register feeds
            NuGetODataV1FeedConfig.Register(config);
            NuGetODataV2CuratedFeedConfig.Register(config);
            NuGetODataV2FeedConfig.Register(config);

            config.EnsureInitialized();
        }
    }
}