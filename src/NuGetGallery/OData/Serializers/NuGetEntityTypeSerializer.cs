// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Formatter.Serialization;
using System.Web.Http.Routing;
using Microsoft.Data.OData;
using Microsoft.Data.OData.Atom;

namespace NuGetGallery.OData.Serializers
{
    public class NuGetEntityTypeSerializer
        : ODataEntityTypeSerializer
    {
        public NuGetEntityTypeSerializer(ODataSerializerProvider serializerProvider)
            : base(serializerProvider)
        {
        }

        public override ODataEntry CreateEntry(SelectExpandNode selectExpandNode, EntityInstanceContext entityInstanceContext)
        {
            var entry = base.CreateEntry(selectExpandNode, entityInstanceContext);

            TryAnnotateV1FeedPackage(entry, entityInstanceContext);
            TryAnnotateV2FeedPackage(entry, entityInstanceContext);

            return entry;
        }

        private void TryAnnotateV1FeedPackage(ODataEntry entry, EntityInstanceContext entityInstanceContext)
        {
            var instance = entityInstanceContext.EntityInstance as V1FeedPackage;
            if (instance != null)
            {
                // Set Atom entry metadata
                entry.SetAnnotation(new AtomEntryMetadata()
                {
                    Title = instance.Id,
                    Authors = new[] { new AtomPersonMetadata { Name = instance.Authors } },
                    Updated = instance.LastUpdated,
                    Summary = instance.Summary
                });

                // Add package download link
                entry.MediaResource = new ODataStreamReferenceValue
                {
                    ContentType = ContentType,
                    ReadLink = BuildLinkForStreamProperty("v1", instance.Id, instance.Version, entityInstanceContext.Request)
                };
            }
        }

        private void TryAnnotateV2FeedPackage(ODataEntry entry, EntityInstanceContext entityInstanceContext)
        {
            var instance = entityInstanceContext.EntityInstance as V2FeedPackage;
            if (instance != null)
            {
                // Set Atom entry metadata
                entry.SetAnnotation(new AtomEntryMetadata()
                {
                    Title = instance.Id,
                    Authors = new[] { new AtomPersonMetadata { Name = instance.Authors } },
                    Updated = instance.LastUpdated,
                    Summary = instance.Summary
                });

                // Add package download link
                entry.MediaResource = new ODataStreamReferenceValue
                {
                    ContentType = ContentType,
                    ReadLink = BuildLinkForStreamProperty("v2", instance.Id, instance.Version, entityInstanceContext.Request)
                };
            }
        }

        public string ContentType
        {
            get { return "application/zip"; }
        }

        private Uri BuildLinkForStreamProperty(string routePrefix, string id, string version, HttpRequestMessage request)
        {
            var url = new UrlHelper(request);
            var result = url.Route(routePrefix + RouteName.DownloadPackage, new { id, version });

            var builder = new UriBuilder(request.RequestUri);
            builder.Path = version == null ? EnsureTrailingSlash(result) : result;
            builder.Query = string.Empty;

            return builder.Uri;
        }

        private static string EnsureTrailingSlash(string url)
        {
            if (url != null && !url.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                return url + '/';
            }

            return url;
        }
    }
}