﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using DadsEnergyReporter.Exceptions;
using DadsEnergyReporter.Remote.Common;

namespace DadsEnergyReporter.Remote.OrangeRockland.Client {

    public interface GreenButtonClient {

        Task<XDocument> fetchGreenButtonData();

    }

    internal class GreenButtonClientImpl: AbstractResource, GreenButtonClient {

        private readonly OrangeRocklandClientImpl client;

        public GreenButtonClientImpl(OrangeRocklandClientImpl client): base(client.apiClient) {
            this.client = client;
        }

        public async Task<XDocument> fetchGreenButtonData() {
            UriBuilder uri = OrangeRocklandClientImpl.apiRoot
                .WithPathSegment("Billing")
                .WithPathSegment("GreenButtonData.aspx");

            IDictionary<string, string> hiddenFormData;
            try {
                hiddenFormData = await client.fetchHiddenFormData(uri.Uri);
            } catch (HttpRequestException e) {
                throw new OrangeRocklandException(
                    "Failed to download Green Button data: could not load HTML page before XML request", e);
            }

            var formValues = new List<KeyValuePair<string, string>> {
                new KeyValuePair<string, string>("OptEnergy", "E"),
                new KeyValuePair<string, string>("optFileFormat", "XML"),
                new KeyValuePair<string, string>("imgGreenButton.x", "1"),
                new KeyValuePair<string, string>("imgGreenButton.y", "1")
            };
            formValues.AddRange(hiddenFormData);

            try {
                using HttpResponseMessage response = await httpClient.PostAsync(uri.Uri, new FormUrlEncodedContent(formValues));
                return await readContentAsXml(response);
            } catch (HttpRequestException e) {
                throw new OrangeRocklandException("Failed to download Green Button data: XML request failed", e);
            }
        }

    }

}