﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Luna.Clients.Azure.Auth;
using Luna.Clients.Controller;
using Luna.Clients.Exceptions;
using Luna.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Luna.Clients.Azure.APIM
{
    public class APIVersionAPIM : IAPIVersionAPIM
    {
        private string REQUEST_BASE_URL = "https://lunaai.management.azure-api.net";
        private string PATH_FORMAT = "/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.ApiManagement/service/{2}/apis/{3}";
        private Guid _subscriptionId;
        private string _resourceGroupName;
        private string _apimServiceName;
        private string _token;
        private string _apiVersion;
        private HttpClient _httpClient;
        private IAPIVersionSetAPIM _apiVersionSetAPIM;

        [ActivatorUtilitiesConstructor]
        public APIVersionAPIM(IOptionsMonitor<APIMConfigurationOption> options,
                           HttpClient httpClient,
                           IAPIVersionSetAPIM apiVersionSetAPIM,
                           IKeyVaultHelper keyVaultHelper)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            _subscriptionId = options.CurrentValue.Config.SubscriptionId;
            _resourceGroupName = options.CurrentValue.Config.ResourceGroupname;
            _apimServiceName = options.CurrentValue.Config.APIMServiceName;
            _token = keyVaultHelper.GetSecretAsync(options.CurrentValue.Config.VaultName, options.CurrentValue.Config.Token).Result;
            _apiVersion = options.CurrentValue.Config.APIVersion;
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiVersionSetAPIM = apiVersionSetAPIM;
        }

        private Uri GetAPIVersionAPIMRequestURI(string versionName, IDictionary<string, string> queryParams = null)
        {
            var builder = new UriBuilder(REQUEST_BASE_URL + GetAPIMRESTAPIPath(versionName));

            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (KeyValuePair<string, string> kv in queryParams ?? new Dictionary<string, string>()) query[kv.Key] = kv.Value;
            query["api-version"] = _apiVersion;
            string queryString = query.ToString();

            builder.Query = query.ToString();

            return new Uri(builder.ToString());
        }

        private Models.Azure.APIVersion GetAPIVersion(string type, APIVersion version)
        {
            Models.Azure.APIVersion api = new Models.Azure.APIVersion();
            api.name = version.GetVersionIdFormat();
            api.properties.displayName = version.GetVersionIdFormat();
            api.properties.apiVersion = version.VersionName;

            IController controller = ControllerHelper.GetController(type);
            api.properties.serviceUrl = controller.GetBaseUrl() + controller.GetPath(version.ProductName, version.DeploymentName);
            api.properties.path = GetAPIMPath(version.ProductName, version.DeploymentName);
            api.properties.apiVersionSetId = _apiVersionSetAPIM.GetAPIMRESTAPIPath(version.DeploymentName);

            return api;
        }

        private Models.Azure.APIVersion GetOriginAPIVersion(Deployment deployment)
        {
            Models.Azure.APIVersion api = new Models.Azure.APIVersion();
            api.name = deployment.DeploymentName;
            api.properties.displayName = deployment.DeploymentName;
            api.properties.apiVersion = deployment.DeploymentName;

            api.properties.serviceUrl = "";
            api.properties.path = GetAPIMPath(deployment.ProductName, deployment.DeploymentName);
            api.properties.apiVersionSetId = _apiVersionSetAPIM.GetAPIMRESTAPIPath(deployment.DeploymentName);

            return api;
        }


        public string GetAPIMPath(string productName, string deploymentName)
        {
            return string.Format("{0}/{1}", productName, deploymentName);
        }

        public string GetAPIMRESTAPIPath(string versionName)
        {
            return string.Format(PATH_FORMAT, _subscriptionId, _resourceGroupName, _apimServiceName, versionName);
        }

        public async Task<bool> ExistsAsync(string type, APIVersion version)
        {
            Uri requestUri = GetAPIVersionAPIMRequestURI(version.GetVersionIdFormat());
            var request = new HttpRequestMessage { RequestUri = requestUri, Method = HttpMethod.Get };

            request.Headers.Add("Authorization", _token);
            request.Headers.Add("If-Match", "*");

            request.Content = new StringContent(JsonConvert.SerializeObject(GetAPIVersion(type, version)), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return false;

            Models.Azure.APIVersion apiVersionAPIM = (Models.Azure.APIVersion)System.Text.Json.JsonSerializer.Deserialize(responseContent, typeof(Models.Azure.APIVersion));
            if (apiVersionAPIM == null)
            {
                throw new LunaServerException($"Query result in bad format. The response is {responseContent}.");
            }
            return true;
        }

        public async Task CreateAsync(string type, APIVersion version)
        {
            Uri requestUri = GetAPIVersionAPIMRequestURI(version.GetVersionIdFormat());
            var request = new HttpRequestMessage { RequestUri = requestUri, Method = HttpMethod.Put };

            request.Headers.Add("Authorization", _token);
            request.Headers.Add("If-Match", "*");

            request.Content = new StringContent(JsonConvert.SerializeObject(GetAPIVersion(type, version)), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new LunaServerException($"Query failed with response {responseContent}");
            }
        }

        public async Task UpdateAsync(string type, APIVersion version)
        {
            Uri requestUri = GetAPIVersionAPIMRequestURI(version.GetVersionIdFormat());
            var request = new HttpRequestMessage { RequestUri = requestUri, Method = HttpMethod.Put };

            request.Headers.Add("Authorization", _token);
            request.Headers.Add("If-Match", "*");

            request.Content = new StringContent(JsonConvert.SerializeObject(GetAPIVersion(type, version)), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new LunaServerException($"Query failed with response {responseContent}");
            }
        }

        public async Task DeleteAsync(string type, APIVersion version)
        {
            if (!(await ExistsAsync(type, version))) return;

            Uri requestUri = GetAPIVersionAPIMRequestURI(version.GetVersionIdFormat());
            var request = new HttpRequestMessage { RequestUri = requestUri, Method = HttpMethod.Delete };

            request.Headers.Add("Authorization", _token);
            request.Headers.Add("If-Match", "*");

            request.Content = new StringContent(JsonConvert.SerializeObject(GetAPIVersion(type, version)), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new LunaServerException($"Query failed with response {responseContent}");
            }
        }

        public async Task CreateAsync(Deployment deployment)
        {
            Uri requestUri = GetAPIVersionAPIMRequestURI(deployment.DeploymentName);
            var request = new HttpRequestMessage { RequestUri = requestUri, Method = HttpMethod.Put };

            request.Headers.Add("Authorization", _token);
            request.Headers.Add("If-Match", "*");

            request.Content = new StringContent(JsonConvert.SerializeObject(GetOriginAPIVersion(deployment)), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new LunaServerException($"Query failed with response {responseContent}");
            }
        }

        public async Task UpdateAsync(Deployment deployment)
        {
            Uri requestUri = GetAPIVersionAPIMRequestURI(deployment.DeploymentName);
            var request = new HttpRequestMessage { RequestUri = requestUri, Method = HttpMethod.Put };

            request.Headers.Add("Authorization", _token);
            request.Headers.Add("If-Match", "*");

            request.Content = new StringContent(JsonConvert.SerializeObject(GetOriginAPIVersion(deployment)), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new LunaServerException($"Query failed with response {responseContent}");
            }
        }

        public async Task DeleteAsync(Deployment deployment)
        {
            Uri requestUri = GetAPIVersionAPIMRequestURI(deployment.DeploymentName);
            var request = new HttpRequestMessage { RequestUri = requestUri, Method = HttpMethod.Delete };

            request.Headers.Add("Authorization", _token);
            request.Headers.Add("If-Match", "*");

            request.Content = new StringContent(JsonConvert.SerializeObject(GetOriginAPIVersion(deployment)), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            string responseContent = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new LunaServerException($"Query failed with response {responseContent}");
            }
        }
    }
}