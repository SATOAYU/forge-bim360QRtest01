using Autodesk.Forge;
using Autodesk.Forge.Model;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Hosting;
using System.Net.Http.Headers;

namespace ParamUpdater.Controllers
{
    public class DesignAutomationRevitController : ControllerBase
    {
        private IHostingEnvironment _env;
        public DesignAutomationRevitController(IHostingEnvironment env)
        {
            _env = env;
        }

        public class Input
        {
            public string versionUrl { get; set; }
            public string input { get; set; }
        }

        public class Output
        {
            public string Urn { get; set; }
            public StatusEnum Status { get; set; }
            public string Message { get; set; }

            public Output(StatusEnum status, string message, string urn)
            {
                Status = status;
                Urn = urn;
                Message = message;
            }

            public Output(StatusEnum status, string message)
            {
                Status = status;
                Urn = string.Empty;
                Message = message;
            }

            public enum StatusEnum
            {
                Error,
                Sucess
            }
        }

        private Credentials Credentials { get; set; }

        [HttpPost]
        [Route("api/forge/designautomation/revit/parameter")]
        public async Task<dynamic> UpdateParameters([FromBody]Input input)
        {
            // adjust input, replace " with '
            input.input = input.input.Replace("\"", "'");

            // 3-legged token to access user data
            Credentials = await Credentials.FromSessionAsync(base.Request.Cookies, base.Response.Cookies);
            if (Credentials == null)
                return new Output(Output.StatusEnum.Error, "Invalid credentials");

            // 2-legged token to perform Design Automation Tasks
            TwoLeggedApi oauth = new TwoLeggedApi();
            string grantType = "client_credentials";
            dynamic bearer = await oauth.AuthenticateAsync(Credentials.GetAppSetting("FORGE_CLIENT_ID"), Credentials.GetAppSetting("FORGE_CLIENT_SECRET"), grantType, new Scope[] { Scope.CodeAll });
            string at = bearer.access_token;

            DesignAutomationRevit da = new DesignAutomationRevit(bearer.access_token);

            string nickName = Credentials.GetAppSetting("FORGE_CLIENT_ID");
            const string appName = "UpdateParameterApp";
            const string activityName = "UpdateParameterActivity";
            string alias = "v1";

            // Check if App is defined
            List<string> apps = await da.GetAppBundles(nickName);
            if (!apps.Contains(string.Format("{0}.{1}+{2}", nickName, appName, alias)))
            {
                string packageZipPath = Path.Combine(_env.ContentRootPath, "ChangeParameter.zip");
                if (!System.IO.File.Exists(packageZipPath))
                    return new Output(Output.StatusEnum.Error, "Change Parameter bundle not found at " + packageZipPath);

                var newApp = await da.CreateApp(appName, appName /* repeat name as description */, alias);
                if (newApp == null)
                    return new Output(Output.StatusEnum.Error, "Cannot create new app");

                // upload the zip with .bundle
                RestClient uploadClient = new RestClient(newApp["uploadParameters"]["endpointURL"].Value<string>());
                RestRequest request = new RestRequest(string.Empty, Method.POST);
                request.AlwaysMultipartFormData = true;
                foreach (JProperty x in newApp["uploadParameters"]["formData"])
                    if (!string.IsNullOrEmpty(x.Value.ToString())) // some values are empty, don't add them...
                        request.AddParameter(x.Name, x.Value);

                request.AddFile("file", packageZipPath);
                request.AddHeader("Cache-Control", "no-cache");
                var res = await uploadClient.ExecuteTaskAsync(request);
            }

            // Check if Ativity is defined
            List<string> activities = await da.GetActivities(nickName);
            if (!activities.Contains(string.Format("{0}.{1}+{2}", nickName, activityName, alias)))
            {
                if (await da.CreateActivity(nickName, activityName, appName, alias) == null)
                    return new Output(Output.StatusEnum.Error, "Cannot create activity");
            }

            string[] idParams = input.versionUrl.Split('/');
            string projectId = idParams[idParams.Length - 3];
            string versionId = idParams[idParams.Length - 1];
            string downloadUrl = string.Empty;
            string uploadUrl = string.Empty;

            VersionsApi versionApi = new VersionsApi();
            versionApi.Configuration.AccessToken = Credentials.TokenInternal;
            dynamic version = await versionApi.GetVersionAsync(projectId, versionId);
            dynamic versionItem = await versionApi.GetVersionItemAsync(projectId, versionId);

            string[] versionItemParams = ((string)version.data.relationships.storage.data.id).Split('/');
            string[] bucketKeyParams = versionItemParams[versionItemParams.Length - 2].Split(':');
            string bucketKey = bucketKeyParams[bucketKeyParams.Length - 1];
            string objectName = versionItemParams[versionItemParams.Length - 1];
            downloadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, objectName);

            ItemsApi itemApi = new ItemsApi();
            itemApi.Configuration.AccessToken = Credentials.TokenInternal;
            string itemId = versionItem.data.id;
            dynamic item = await itemApi.GetItemAsync(projectId, itemId);
            string folderId = item.data.relationships.parent.data.id;
            string fileName = item.data.attributes.displayName;

            ProjectsApi projectApi = new ProjectsApi();
            projectApi.Configuration.AccessToken = Credentials.TokenInternal;
            StorageRelationshipsTargetData storageRelData = new StorageRelationshipsTargetData(StorageRelationshipsTargetData.TypeEnum.Folders, folderId);
            CreateStorageDataRelationshipsTarget storageTarget = new CreateStorageDataRelationshipsTarget(storageRelData);
            CreateStorageDataRelationships storageRel = new CreateStorageDataRelationships(storageTarget);
            BaseAttributesExtensionObject attributes = new BaseAttributesExtensionObject(string.Empty, string.Empty, new JsonApiLink(string.Empty), null);
            CreateStorageDataAttributes storageAtt = new CreateStorageDataAttributes(fileName, attributes);
            CreateStorageData storageData = new CreateStorageData(CreateStorageData.TypeEnum.Objects, storageAtt, storageRel);
            CreateStorage storage = new CreateStorage(new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0), storageData);

            dynamic storageCreated = await projectApi.PostStorageAsync(projectId, storage);

            string[] storageIdParams = ((string)storageCreated.data.id).Split('/');
            bucketKeyParams = storageIdParams[storageIdParams.Length - 2].Split(':');
            bucketKey = bucketKeyParams[bucketKeyParams.Length - 1];
            objectName = storageIdParams[storageIdParams.Length - 1];

            uploadUrl = string.Format("https://developer.api.autodesk.com/oss/v2/buckets/{0}/objects/{1}", bucketKey, objectName);

            // prepare workitem
            JObject arguments = new JObject
            {
                new JProperty("activityId", string.Format("{0}.{1}+{2}", nickName, activityName, alias)),
                new JProperty("arguments", new JObject{
                    new JProperty("rvtFile", new JObject{
                        new JProperty("url", downloadUrl),
                        new JProperty("headers",
                            new JObject{
                                new JProperty("Authorization", "Bearer " + Credentials.TokenInternal)
                            }
                        )
                    }),
                    new JProperty("updateParameterInput", new JObject{
                        new JProperty("url", "data:application/json, " + input.input)
                    }),
                    new JProperty("result", new JObject{
                        new JProperty("verb", "PUT"),
                        new JProperty("url", uploadUrl),
                        new JProperty("headers",
                            new JObject{
                                new JProperty("Authorization", "Bearer " + Credentials.TokenInternal)
                            }
                        )
                    })
                })
            };

            // post workitem         
            JObject workitem = await da.WorkItem(activityName, arguments);

            VersionsApi versionsApis = new VersionsApi();
            versionsApis.Configuration.AccessToken = Credentials.TokenInternal;
            CreateVersion newVersionData = new CreateVersion
            (
               new JsonApiVersionJsonapi(JsonApiVersionJsonapi.VersionEnum._0),
               new CreateVersionData
               (
                 CreateVersionData.TypeEnum.Versions,
                 new CreateStorageDataAttributes
                 (
                   fileName,
                   new BaseAttributesExtensionObject
                   (
                     "versions:autodesk.core:File",
                     "1.0",
                     new JsonApiLink(string.Empty),
                     null
                   )
                 ),
                 new CreateVersionDataRelationships
                 (
                    new CreateVersionDataRelationshipsItem
                    (
                      new CreateVersionDataRelationshipsItemData
                      (
                        CreateVersionDataRelationshipsItemData.TypeEnum.Items,
                        item.data.id
                      )
                    ),
                    new CreateItemRelationshipsStorage
                    (
                      new CreateItemRelationshipsStorageData
                      (
                        CreateItemRelationshipsStorageData.TypeEnum.Objects,
                        storageCreated.data.id
                      )
                    )
                 )
               )
            );
            dynamic newVersion = await versionsApis.PostVersionAsync(projectId, newVersionData);

            // prepare the payload
            List<JobPayloadItem> outputs = new List<JobPayloadItem>()
            {
                new JobPayloadItem(JobPayloadItem.TypeEnum.Svf,
                new List<JobPayloadItem.ViewsEnum>()
                {
                    JobPayloadItem.ViewsEnum._2d,
                    JobPayloadItem.ViewsEnum._3d
                })
            };
            JobPayload job;
            job = new JobPayload(new JobPayloadInput(Base64Encode(newVersion.data.id)), new JobPayloadOutput(outputs));

            // start the translation
            DerivativesApi derivative = new DerivativesApi();
            derivative.Configuration.AccessToken = Credentials.TokenInternal;
            dynamic jobPosted = await derivative.TranslateAsync(job);

            return new Output(Output.StatusEnum.Sucess, "Job completed", newVersion.data.id);
        }

        /// <summary>
        /// Base64 enconde a string
        /// </summary>
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes).Replace("/", "_");
        }
    }


    public class DesignAutomationRevit
    {
        private RestClient _client;

        public DesignAutomationRevit(string token)
        {
            _client = new RestClient("https://developer.api.autodesk.com");
            _client.AddDefaultHeader("Authorization", string.Format("Bearer {0}", token));
            _client.AddDefaultHeader("Content-Type", "application/json");
        }

        public async Task<List<string>> GetAppBundles(string nickName)
        {
            RestRequest request = new RestRequest("da/us-east/v3/appbundles", Method.GET);
            IRestResponse response = await _client.ExecuteTaskAsync(request);

            JArray appNames = JArray.FromObject(JObject.Parse(response.Content)["data"]);
            return (from JToken n in appNames where n.Value<string>().IndexOf(nickName) > -1 select n.Value<string>()).ToList<string>();
        }

        public async Task<JObject> CreateApp(string id, string description, string alias)
        {
            // create the app
            RestRequest requestCreateApp = new RestRequest("da/us-east/v3/appbundles", Method.POST);
            requestCreateApp.RequestFormat = DataFormat.Json;
            requestCreateApp.AddBody(new { id = id, engine = "Autodesk.Revit+2018", description = description });
            IRestResponse responseApp = await _client.ExecuteTaskAsync(requestCreateApp);
            if (responseApp.StatusCode != HttpStatusCode.OK)
                return null;
            JObject ret = JObject.Parse(responseApp.Content);

            // create alias
            RestRequest requestCreateAlias = new RestRequest(string.Format("da/us-east/v3/appbundles/{0}/aliases", id), Method.POST);
            requestCreateAlias.RequestFormat = DataFormat.Json;
            requestCreateAlias.AddBody(new { version = 1, id = alias });
            IRestResponse responseAlias = await _client.ExecuteTaskAsync(requestCreateAlias);

            return ret;
        }

        public async Task<List<string>> GetActivities(string nickName)
        {
            RestRequest request = new RestRequest("da/us-east/v3/activities", Method.GET);
            IRestResponse response = await _client.ExecuteTaskAsync(request);

            JArray appNames = JArray.FromObject(JObject.Parse(response.Content)["data"]);
            return (from JToken n in appNames where n.Value<string>().IndexOf(nickName) > -1 select n.Value<string>()).ToList<string>();
        }

        public async Task<JObject> CreateActivity(string nickName, string activityName, string appName, string alias)
        {
            JObject body = JObject.Parse(@"
                {
                'id':'',
                'commandLine':[
                ],
                'parameters':{
                    'rvtFile':{
                        'zip':false,
                        'ondemand':false,
                        'verb':'get',
                        'description':'Input Revit model',
                        'required':true,
                        'localName':'$(rvtFile)'
                    },
                    'updateParameterInput':{
                        'zip':false,
                        'ondemand':false,
                        'verb':'get',
                        'description':'',
                        'required':false,
                        'localName':'params.json'
                    },
                    'result':{
                        'zip':false,
                        'ondemand':false,
                        'verb':'put',
                        'description':'Results',
                        'required':true,
                        'localName':'result.rvt'
                    }
                },
                'engine':'Autodesk.Revit+2018',
                'appbundles':[
                ],
                'description':''
                }
            ");

            body["id"] = activityName;
            ((JArray)body["commandLine"]).Add(string.Format(@"$(engine.path)\\revitcoreconsole.exe /i $(args[rvtFile].path) /al $(appbundles[{1}].path)", nickName, appName, alias));
            ((JArray)body["appbundles"]).Add(string.Format("{0}.{1}+{2}", nickName, appName, alias));

            // create activity
            RestRequest requestCreateActivity = new RestRequest("da/us-east/v3/activities", Method.POST);
            requestCreateActivity.AddParameter("application/json", body.ToString(Formatting.None), ParameterType.RequestBody);
            IRestResponse responseCreateActivity = await _client.ExecuteTaskAsync(requestCreateActivity);
            if (responseCreateActivity.StatusCode != HttpStatusCode.OK) return null;

            // create alias
            RestRequest requestCreateAlias = new RestRequest(string.Format("da/us-east/v3/activities/{0}/aliases", activityName), Method.POST);
            requestCreateAlias.RequestFormat = DataFormat.Json;
            requestCreateAlias.AddBody(new { version = 1, id = alias });
            IRestResponse responseAlias = await _client.ExecuteTaskAsync(requestCreateAlias);

            return JObject.Parse(responseCreateActivity.Content);
        }

        public async Task<JObject> WorkItem(string activityName, JObject arguments)
        {
            // post workitem
            RestRequest requestWorkitem = new RestRequest("da/us-east/v3/workitems", Method.POST);
            requestWorkitem.AddParameter("application/json", arguments.ToString(Formatting.None), ParameterType.RequestBody);
            IRestResponse responseWorkitem = await _client.ExecuteTaskAsync(requestWorkitem);
            if (responseWorkitem.StatusCode != HttpStatusCode.OK) return null;

            dynamic workitem = JObject.Parse(responseWorkitem.Content);

            for (int i = 0; i < 1000; i++)
            {
                System.Threading.Thread.Sleep(1000);
                RestRequest requestWorkitemStatus = new RestRequest(string.Format("da/us-east/v3/workitems/{0}", workitem.id), Method.GET);
                IRestResponse responseStatus = await _client.ExecuteTaskAsync(requestWorkitemStatus);
                if (responseStatus.StatusCode != HttpStatusCode.OK) return null;
                dynamic status = JObject.Parse(responseStatus.Content);
                if (status.status == "pending") continue;
                return JObject.Parse(responseStatus.Content);
            }
            return null;
        }
    }
}