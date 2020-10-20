﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web.TokenCacheProviders;

namespace Microsoft.Identity.Web
{
    /// <summary>
    /// Implementation of ITokenAcquisition for App services authentication (EasyAuth).
    /// </summary>
    public class AppServicesAuthenticationTokenAcquisition : ITokenAcquisition
    {
        private IConfidentialClientApplication _confidentialClientApplication;
        private IHttpContextAccessor _httpContextAccessor;
        private IMsalHttpClientFactory _httpClientFactory;
        private IMsalTokenCacheProvider _tokenCacheProvider;

        private HttpContext? CurrentHttpContext
        {
            get
            {
                return _httpContextAccessor.HttpContext;
            }
        }

        /// <summary>
        /// Constructor of the AppServicesAuthenticationTokenAcquisition.
        /// </summary>
        /// <param name="tokenCacheProvider">The App token cache provider.</param>
        /// <param name="httpContextAccessor">Access to the HttpContext of the request.</param>
        /// <param name="httpClientFactory">HTTP client factory.</param>
        public AppServicesAuthenticationTokenAcquisition(IMsalTokenCacheProvider tokenCacheProvider,  IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _httpClientFactory = new MsalAspNetCoreHttpClientFactory(httpClientFactory);
            _tokenCacheProvider = tokenCacheProvider;
        }

        private async Task<IConfidentialClientApplication> GetOrCreateApplication()
        {
            if (_confidentialClientApplication == null)
            {
                ConfidentialClientApplicationOptions options = new ConfidentialClientApplicationOptions()
                {
                    ClientId = AppServiceAuthenticationInformation.ClientId,
                    ClientSecret = AppServiceAuthenticationInformation.ClientSecret,
                    Instance = AppServiceAuthenticationInformation.Issuer,
                };
                _confidentialClientApplication = ConfidentialClientApplicationBuilder.CreateWithApplicationOptions(options)
                    .WithHttpClientFactory(_httpClientFactory)
                    .Build();
                await _tokenCacheProvider.InitializeAsync(_confidentialClientApplication.AppTokenCache).ConfigureAwait(false);
                await _tokenCacheProvider.InitializeAsync(_confidentialClientApplication.UserTokenCache).ConfigureAwait(false);
            }
            return _confidentialClientApplication;
        }

        /// <inheritdoc/>
        public async Task<string> GetAccessTokenForAppAsync(
            string scope,
            string? tenant = null,
            TokenAcquisitionOptions? tokenAcquisitionOptions = null)
        {
            // We could use MSI
            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            var app = await GetOrCreateApplication();
            AuthenticationResult result = await app.AcquireTokenForClient(new string[] { scope })
                .ExecuteAsync()
                .ConfigureAwait(false);

            return result.AccessToken;
        }

        /// <inheritdoc/>
        public Task<string> GetAccessTokenForUserAsync(IEnumerable<string> scopes, string? tenantId = null, string? userFlow = null, ClaimsPrincipal? user = null, TokenAcquisitionOptions? tokenAcquisitionOptions = null)
        {
            string accessToken = GetAccessToken(CurrentHttpContext.Request.Headers);
            return Task.FromResult(accessToken);
        }

        private string? GetAccessToken(IHeaderDictionary? headers)
        {
            const string easyAuthAccessTokenHeader = "X-MS-TOKEN-AAD-ACCESS-TOKEN";

            string? accessToken = null;
            if (headers != null)
            {
                accessToken = headers[easyAuthAccessTokenHeader];
            }
#if DEBUG
            if (string.IsNullOrEmpty(accessToken))
            {
                accessToken = AppServiceAuthenticationInformation.SimulateGetttingHeaderFromDebugEnvironmentVariable(easyAuthAccessTokenHeader);
            }
#endif
            return accessToken;
        }

        /// <inheritdoc/>
        public async Task<AuthenticationResult> GetAuthenticationResultForUserAsync(IEnumerable<string> scopes, string? tenantId = null, string? userFlow = null, ClaimsPrincipal? user = null, TokenAcquisitionOptions? tokenAcquisitionOptions = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public async Task ReplyForbiddenWithWwwAuthenticateHeaderAsync(IEnumerable<string> scopes, MsalUiRequiredException msalServiceException, HttpResponse? httpResponse = null)
        {
            // Not implmented for the moment
            throw new NotImplementedException();
        }
    }
}
