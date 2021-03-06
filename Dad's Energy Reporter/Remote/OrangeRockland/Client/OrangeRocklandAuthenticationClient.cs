﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DadsEnergyReporter.Data.Marshal;
using DadsEnergyReporter.Exceptions;
using DadsEnergyReporter.Remote.Common;

namespace DadsEnergyReporter.Remote.OrangeRockland.Client {

    public interface OrangeRocklandAuthenticationClient {

        Task<OrangeRocklandAuthToken> submitCredentials(string username, string password);

        Task logOut();

    }

    internal class OrangeRocklandAuthenticationClientImpl: AbstractResource, OrangeRocklandAuthenticationClient {

        private const string AUTH_COOKIE_NAME = "LogCOOKPl95FnjAT";

        public OrangeRocklandAuthenticationClientImpl(OrangeRocklandClientImpl client): base(client.apiClient) { }

        public async Task logOut() {
            UriBuilder uri = OrangeRocklandClientImpl.apiRoot
                .WithPathSegment("logoff.aspx");

            try {
                using HttpResponseMessage response = await httpClient.GetAsync(uri.Uri);
                response.EnsureSuccessStatusCode();
            } catch (HttpRequestException e) {
                throw new OrangeRocklandException("Failed to log out", e);
            }
        }

        public async Task<OrangeRocklandAuthToken> submitCredentials(string username, string password) {
            Uri tokenExchangeUri = await fetchTokenExchangeUriWithCredentials(username, password);
            OrangeRocklandAuthToken authToken = await fetchAuthToken(tokenExchangeUri);
            await activateSession();

            return authToken;
        }

        private async Task<Uri> fetchTokenExchangeUriWithCredentials(string username, string password) {
            var credentialSubmissionUri =
                new Uri("https://www.oru.com/sitecore/api/ssc/ConEd-Cms-Services-Controllers-Okta/User/0/Login");

            try {
                var credentialParams = new Dictionary<string, object> {
                    { "LoginEmail", username },
                    { "LoginPassword", password },
                    { "LoginRememberMe", false },
                    { "ReturnUrl", "" }
                };

                using HttpResponseMessage response =
                    await httpClient.PostAsync(credentialSubmissionUri, new JsonEncodedContent(credentialParams));
                IDictionary<string, object> responseBody =
                    await apiClient.contentHandlers.readContentAsJson<IDictionary<string, object>>(response);
                response.EnsureSuccessStatusCode();

                string authRedirectUrl = (string) responseBody["authRedirectUrl"];
                if (authRedirectUrl.Contains("ForgotPassword")) {
                    throw new OrangeRocklandException("Auth Phase 1/2: Incorrect username or password.");
                }

                return new Uri(authRedirectUrl);
            } catch (HttpRequestException e) {
                throw new OrangeRocklandException(
                    "Auth Phase 1/3: Failed to log in with credentials, Orange and Rockland site may be unavailable.",
                    e);
            }
        }

        private async Task<OrangeRocklandAuthToken> fetchAuthToken(Uri tokenExchangeUri) {
            try {
                using HttpResponseMessage response = await httpClient.GetAsync(tokenExchangeUri);
                response.EnsureSuccessStatusCode();
                Cookie logInCookie = apiClient.cookies.GetCookies(tokenExchangeUri)[AUTH_COOKIE_NAME];

                if (logInCookie == null) {
                    throw new OrangeRocklandException(
                        $"Auth Phase 2/3: No {AUTH_COOKIE_NAME} cookie was set after submitting credentials, username or password may be incorrect.");
                }

                return new OrangeRocklandAuthToken { logInCookie = logInCookie.Value };
            } catch (HttpRequestException e) {
                throw new OrangeRocklandException(
                    "Auth Phase 2/3: Failed to log in with credentials, Orange and Rockland site may be unavailable.",
                    e);
            }
        }

        private async Task activateSession() {
            UriBuilder accountStatusUri = OrangeRocklandClientImpl.apiRoot
                .WithPathSegment("System")
                .WithPathSegment("accountStatus.aspx");

            try {
                /*
                 * This request looks useless, but it actually makes our session valid.
                 * Without this Account Status request, any subsequent requests would redirect to the log in page,
                 * even though we already have a session ID from logging in successfully.
                 */
                using HttpResponseMessage response = await httpClient.GetAsync(accountStatusUri.Uri);
                response.EnsureSuccessStatusCode();
            } catch (HttpRequestException e) {
                throw new OrangeRocklandException(
                    "Auth Phase 3/3: Failed to load account status page, Orange and Rockland site may be unavailable.",
                    e);
            }
        }

    }

}