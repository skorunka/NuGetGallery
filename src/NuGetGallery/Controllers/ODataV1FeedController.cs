﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using NuGetGallery.Configuration;
using NuGetGallery.OData;
using WebApi.OutputCache.V2;

// ReSharper disable once CheckNamespace
namespace NuGetGallery.Controllers
{
    public class ODataV1FeedController 
        : NuGetODataController
    {
        private readonly IEntityRepository<Package> _packagesRepository;
        private readonly ConfigurationService _configurationService;
        private readonly ISearchService _searchService;

        private static readonly ODataQuerySettings SearchQuerySettings = new ODataQuerySettings 
        { 
             HandleNullPropagation = HandleNullPropagationOption.False,
             EnsureStableOrdering = true
        };

        public ODataV1FeedController(IEntityRepository<Package> packagesRepository, ConfigurationService configurationService, ISearchService searchService)
            : base(configurationService)
        {
            _packagesRepository = packagesRepository;
            _configurationService = configurationService;
            _searchService = searchService;
        }

        [HttpGet, HttpPost, EnableQuery(PageSize = SearchAdaptor.MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true)]
        public IQueryable<V1FeedPackage> Get()
        {
            var packages = _packagesRepository.GetAll()
                .Where(p => !p.IsPrerelease)
                .WithoutVersionSort()
                .ToV1FeedPackageQuery(_configurationService.GetSiteRoot(UseHttps()));

            return packages;
        }

        [HttpGet]
        public HttpResponseMessage GetCount(ODataQueryOptions<V1FeedPackage> options)
        {
            var queryResults = (IQueryable<V1FeedPackage>)options.ApplyTo(Get());
            var count = queryResults.Count();

            return CountResult(count);
        }

        [HttpGet, EnableQuery(PageSize = SearchAdaptor.MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true)]
        public async Task<IHttpActionResult> Get(string id, string version)
        {
            return await GetCore(id, version);
        }

        [HttpGet, HttpPost, EnableQuery(PageSize = SearchAdaptor.MaxPageSize, HandleNullPropagation = HandleNullPropagationOption.False, EnsureStableOrdering = true)]
        public async Task<IHttpActionResult> FindPackagesById([FromODataUri]string id)
        {
            return await GetCore(id, null);
        }

        private async Task<IHttpActionResult> GetCore(string id, string version)
        {
            // todo: route through search service?

            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Parameter 'id' must be specified.");
            }

            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Where(p => p.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && !p.IsPrerelease);

            if (!string.IsNullOrEmpty(version))
            {
                packages = packages.Where(p => p.Version == version); // todo: normalizedversion?
            }

            if (_configurationService.Features.PackageRestoreViaSearch)
            {
                try
                {
                    packages = await SearchAdaptor.FindByIdCore(_searchService, GetTraditionalHttpContext().Request, packages, id, curatedFeed: null);
                }
                catch (Exception ex)
                {
                    // Swallowing Exception intentionally. If *anything* goes wrong in search, just fall back to the database.
                    // We don't want to break package restores. We do want to know if this happens, so here goes:
                    QuietLog.LogHandledException(ex);
                }
            }

            var query = packages.ToV1FeedPackageQuery(GetSiteRoot());
            return Ok(query);
        }

        [HttpGet, HttpPost, CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTime)]
        public async Task<IEnumerable<V1FeedPackage>> Search(ODataQueryOptions<V1FeedPackage> queryOptions, [FromODataUri]string searchTerm = "", [FromODataUri]string targetFramework = "")
        {
            // Ensure we can provide paging
            var pageSize = queryOptions.Top != null ? (int?)null : SearchAdaptor.MaxPageSize;
            var settings = new ODataQuerySettings(SearchQuerySettings) { PageSize = pageSize };

            // Handle OData-style |-separated list of frameworks.
            string[] targetFrameworkList = (targetFramework ?? "").Split(new[] { '\'', '|' }, StringSplitOptions.RemoveEmptyEntries);

            // For now, we'll just filter on the first one.
            if (targetFrameworkList.Length > 0)
            {
                // Until we support multiple frameworks, we need to prefer aspnet50 over aspnetcore50.
                if (targetFrameworkList.Contains("aspnet50"))
                {
                    targetFramework = "aspnet50";
                }
                else
                {
                    targetFramework = targetFrameworkList[0];
                }
            }
            
            // Peform actual search
            var packages = _packagesRepository.GetAll()
                .Include(p => p.PackageRegistration)
                .Include(p => p.PackageRegistration.Owners)
                .Where(p => p.Listed && !p.IsPrerelease);

            // todo: search hijack should take queryOptions instead of manually parsing query options
            var query = await SearchAdaptor.SearchCore(_searchService, GetTraditionalHttpContext().Request, packages, searchTerm, targetFramework, false, curatedFeed: null);

            var totalHits = query.LongCount();
            var convertedQuery = query
                .ToV1FeedPackageQuery(GetSiteRoot());

            // apply OData query options + limit total of entries explicitly
            convertedQuery = (IQueryable<V1FeedPackage>)queryOptions.ApplyTo(
                convertedQuery.Take(pageSize ?? SearchAdaptor.MaxPageSize)); 

            var nextLink = SearchAdaptor.GetNextLink(Request.RequestUri, convertedQuery, new { searchTerm, targetFramework }, queryOptions, settings, false);

            return new PageResult<V1FeedPackage>(convertedQuery, nextLink, totalHits);
        }

        [HttpGet, CacheOutput(ServerTimeSpan = NuGetODataConfig.SearchCacheTime)]
        public async Task<HttpResponseMessage> SearchCount(ODataQueryOptions<V1FeedPackage> queryOptions, [FromODataUri] string searchTerm = "", [FromODataUri] string targetFramework = "")
        {
            var queryResults = await Search(queryOptions, searchTerm, targetFramework);

            var pageResult = queryResults as PageResult;
            if (pageResult != null && pageResult.Count.HasValue)
            {
                return CountResult(pageResult.Count.Value);
            }

            return CountResult(queryResults.LongCount());
        }
    }
}