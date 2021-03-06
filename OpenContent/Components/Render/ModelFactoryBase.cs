﻿using DotNetNuke.Common;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Services.Localization;
using Newtonsoft.Json.Linq;
using Satrabel.OpenContent.Components.Alpaca;
using Satrabel.OpenContent.Components.Datasource;
using Satrabel.OpenContent.Components.Datasource.Search;
using Satrabel.OpenContent.Components.Handlebars;
using Satrabel.OpenContent.Components.Json;
using Satrabel.OpenContent.Components.Manifest;
using Satrabel.OpenContent.Components.TemplateHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;

namespace Satrabel.OpenContent.Components.Render
{
    public abstract class ModelFactoryBase
    {
        private readonly string _settingsJson;
        private readonly string _physicalTemplateFolder;
        protected readonly TemplateFiles _templateFiles;
        protected readonly int _portalId;
        private readonly string _cultureCode;
        protected readonly string _collection;

        protected JObject _schemaJson = null;
        protected JObject _optionsJson = null;
        private JObject _additionalData = null;

        protected IDataSource _ds;
        protected DataSourceContext _dsContext;

        // only multiple
        protected readonly Manifest.Manifest _manifest;
        protected readonly TemplateManifest _templateManifest;
        protected readonly PortalSettings _portalSettings;
        protected readonly OpenContentModuleInfo _module;
        protected readonly int _detailTabId;

        public ModelFactoryBase(string settingsJson, string physicalTemplateFolder, Manifest.Manifest manifest, TemplateManifest templateManifest, TemplateFiles templateFiles, OpenContentModuleInfo module, PortalSettings portalSettings)

        {
            this._settingsJson = settingsJson;
            this._physicalTemplateFolder = physicalTemplateFolder;
            this._manifest = manifest;
            this._templateFiles = templateFiles;
            this._module = module;
            this._portalSettings = portalSettings;
            this._portalId = portalSettings.PortalId;
            this._templateManifest = templateManifest;
            this._collection = templateManifest == null ? AppConfig.DEFAULT_COLLECTION : templateManifest.Collection;
            this._detailTabId = DnnUtils.GetTabByCurrentCulture(this._portalId, module.GetDetailTabId(), GetCurrentCultureCode());

            _ds = DataSourceManager.GetDataSource(_manifest.DataSource);
            _dsContext = OpenContentUtils.CreateDataContext(_module);
        }

        public ModelFactoryBase(string settingsJson, string physicalTemplateFolder, Manifest.Manifest manifest, TemplateManifest templateManifest, TemplateFiles templateFiles, OpenContentModuleInfo module, int portalId, string cultureCode)

        {
            this._settingsJson = settingsJson;
            this._physicalTemplateFolder = physicalTemplateFolder;
            this._manifest = manifest;
            this._templateFiles = templateFiles;
            this._module = module;
            this._cultureCode = cultureCode;
            this._portalId = portalId;
            this._templateManifest = templateManifest;
            this._collection = templateManifest.Collection;
            this._detailTabId = DnnUtils.GetTabByCurrentCulture(this._portalId, module.GetDetailTabId(), GetCurrentCultureCode());
            _ds = DataSourceManager.GetDataSource(_manifest.DataSource);
            _dsContext = OpenContentUtils.CreateDataContext(_module);
        }

        public ModelFactoryBase(OpenContentModuleInfo module, PortalSettings portalSettings)
        {
            OpenContentSettings settings = module.Settings;
            this._settingsJson = settings.Data;
            this._physicalTemplateFolder = settings.Template.ManifestFolderUri.PhysicalFullDirectory + "\\";
            this._manifest = settings.Template.Manifest;
            this._templateFiles = settings.Template?.Main;
            this._module = module;
            this._portalSettings = portalSettings;
            this._portalId = portalSettings.PortalId;
            this._templateManifest = settings.Template;
            this._collection = _templateManifest.Collection;
            this._detailTabId = DnnUtils.GetTabByCurrentCulture(this._portalId, module.GetDetailTabId(), GetCurrentCultureCode());
            _ds = DataSourceManager.GetDataSource(_manifest.DataSource);
            _dsContext = OpenContentUtils.CreateDataContext(_module);
        }
        public ModelFactoryBase(OpenContentModuleInfo module, PortalSettings portalSettings, string collection)
        {
            OpenContentSettings settings = module.Settings;
            this._settingsJson = settings.Data;
            this._physicalTemplateFolder = settings.Template.ManifestFolderUri.PhysicalFullDirectory + "\\";
            this._manifest = settings.Template.Manifest;
            this._templateFiles = settings.Template?.Main;
            this._module = module;
            this._portalSettings = portalSettings;
            this._portalId = portalSettings.PortalId;
            this._templateManifest = settings.Template;
            this._collection = collection;
            this._detailTabId = DnnUtils.GetTabByCurrentCulture(this._portalId, module.GetDetailTabId(), GetCurrentCultureCode());
            _ds = DataSourceManager.GetDataSource(_manifest.DataSource);
            _dsContext = OpenContentUtils.CreateDataContext(_module);
        }

        public JObject Options { get; set; } // alpaca options.json format


        public dynamic GetModelAsDynamic(bool onlyData = false)
        {
            if (_portalSettings == null) onlyData = true;

            JToken model = GetModelAsJson(onlyData);
            return JsonUtils.JsonToDynamic(model.ToString());
        }
        public Dictionary<string, object> GetModelAsDictionary(bool onlyData = false, bool onlyMainData = false)
        {
            if (_portalSettings == null) onlyData = true;

            JToken model = GetModelAsJson(onlyData, onlyMainData);
            return JsonUtils.JsonToDictionary(model.ToString());
        }

        public abstract JToken GetModelAsJson(bool onlyData = false, bool onlyMainData = false);

        protected void EnhanceSelect2(JObject model, bool onlyData)
        {
            string colName = string.IsNullOrEmpty(_collection) ? "Items" : _collection;
            bool addDataEnhance = _manifest.AdditionalDataDefined();
            if (addDataEnhance && _additionalData == null)
            {
                GetAdditionalData();
            }
            bool collectonEnhance = _templateFiles != null && _templateFiles.Model != null && _templateFiles.Model.ContainsKey(colName);
            bool enhance = addDataEnhance || collectonEnhance || _templateFiles.LabelsInTemplate;
            if (enhance && (_optionsJson == null || _schemaJson == null))
            {
                var alpaca = _ds.GetAlpaca(_dsContext, true, true, false);
                {
                    _schemaJson = alpaca["schema"] as JObject; // cache
                    _optionsJson = alpaca["options"] as JObject; // cache
                }
            }
            if (enhance)
            {
                var colManifest = collectonEnhance ? _templateFiles.Model[colName] : null;
                var includes = colManifest?.Includes;
                var includelabels = _templateFiles != null && _templateFiles.LabelsInTemplate;
                var ds = DataSourceManager.GetDataSource(_manifest.DataSource);
                var dsContext = OpenContentUtils.CreateDataContext(_module);
                JsonUtils.LookupJson(model, _additionalData, _schemaJson, _optionsJson, includelabels, includes,
                    (col, id) =>
                    {
                        // collection enhancement
                        dsContext.Collection = col;
                        var dsItem = ds.Get(dsContext, id);
                        if (dsItem != null && dsItem.Data is JObject)
                        {
                            return dsItem.Data as JObject;
                        }
                        else
                        {
                            JObject res = new JObject();
                            res["Id"] = id;
                            res["Collection"] = col;
                            res["Title"] = "unknow";
                            return res;
                        }
                    },
                    (key) =>
                    {
                        return ds.GetDataAlpaca(dsContext, true, true, false, key);
                    });
            }
            if (_optionsJson != null)
            {
                LookupSelect2InOtherModule(model, _optionsJson, onlyData);
            }
        }

        

        protected void ExtendModel(JObject model, bool onlyData, bool onlyMainData)
        {
            if (_portalSettings == null) onlyData = true;

            if (_templateFiles != null)
            {
                // include additional data in the Model
                if (_templateFiles.AdditionalDataInTemplate && _manifest.AdditionalDataDefined())
                {
                    model["AdditionalData"] = GetAdditionalData();
                }
                // include collections
                if (_templateFiles.Model != null)
                {
                    var additionalCollections = _templateFiles.Model.Where(c => c.Key != _collection);
                    if (additionalCollections.Any())
                    {
                        var collections = model["Collections"] = new JObject();
                        var dsColContext = OpenContentUtils.CreateDataContext(_module);
                        foreach (var item in additionalCollections)
                        {
                            var colManifest = item.Value;
                            dsColContext.Collection = item.Key;
                            Select select = null;
                            if (item.Value.Query != null)
                            {
                                var indexConfig = OpenContentUtils.GetIndexConfig(_module.Settings.TemplateDir, item.Key);
                                QueryBuilder queryBuilder = new QueryBuilder(indexConfig);
                                var u = PortalSettings.Current.UserInfo;
                                queryBuilder.Build(item.Value.Query, true, u.UserID, DnnLanguageUtils.GetCurrentCultureCode(), u.Social.Roles);
                                select = queryBuilder.Select;
                            }
                            IDataItems dataItems = _ds.GetAll(dsColContext, select);
                            var colDataJson = new JArray();
                            foreach (var dataItem in dataItems.Items)
                            {
                                var json = dataItem.Data;
                                if (json is JObject)
                                {
                                    JObject context = new JObject();
                                    json["Context"] = context;
                                    context["Id"] = dataItem.Id;
                                    EnhanceSelect2(json as JObject, onlyData);
                                    JsonUtils.SimplifyJson(json, GetCurrentCultureCode());
                                }
                                colDataJson.Add(json);
                            }
                            collections[item.Key] = new JObject();
                            collections[item.Key]["Items"] = colDataJson;
                        }
                    }
                }
            }
            // include settings in the Model
            if (!onlyMainData && _templateManifest.SettingsNeeded() && !string.IsNullOrEmpty(_settingsJson))
            {
                try
                {
                    var jsonSettings = JToken.Parse(_settingsJson);
                    JsonUtils.SimplifyJson(jsonSettings, GetCurrentCultureCode());
                    model["Settings"] = jsonSettings;
                }
                catch (Exception ex)
                {
                    throw new Exception("Error parsing Json of Settings", ex);
                }
            }

            // include static localization in the Model
            if (!onlyMainData)
            {
                JToken localizationJson = null;
                string localizationFilename = _physicalTemplateFolder + GetCurrentCultureCode() + ".json";
                if (File.Exists(localizationFilename))
                {
                    string fileContent = File.ReadAllText(localizationFilename);
                    if (!string.IsNullOrWhiteSpace(fileContent))
                    {
                        localizationJson = fileContent.ToJObject("Localization: " + localizationFilename);
                    }
                }
                if (localizationJson != null)
                {
                    model["Localization"] = localizationJson;
                }
            }
            if (!onlyData)
            {
                // include CONTEXT in the Model
                JObject context = new JObject();
                model["Context"] = context;
                context["ModuleId"] = _module.ViewModule.ModuleID;
                context["TabId"] = _module.ViewModule.TabID;
                context["GoogleApiKey"] = OpenContentControllerFactory.Instance.OpenContentGlobalSettingsController(_portalId).GetGoogleApiKey();
                context["ModuleTitle"] = _module.ViewModule.ModuleTitle;
                var editIsAllowed = !_manifest.DisableEdit && IsEditAllowed(-1);
                context["IsEditable"] = editIsAllowed; //allowed to edit the item or list (meaning allow Add)
                context["IsEditMode"] = IsEditMode;
                context["PortalId"] = _portalId;
                context["MainUrl"] = Globals.NavigateURL(_detailTabId, false, _portalSettings, "", GetCurrentCultureCode());
                context["HomeDirectory"] = _portalSettings.HomeDirectory;
                context["HTTPAlias"] = _portalSettings.PortalAlias.HTTPAlias;
            }
        }

        private JObject GetAdditionalData()
        {
            if (_additionalData == null && _manifest.AdditionalDataDefined())
            {
                _additionalData = new JObject();
                foreach (var item in _manifest.AdditionalDataDefinition)
                {
                    var dataManifest = item.Value;
                    IDataItem dataItem = _ds.GetData(_dsContext, dataManifest.ScopeType, dataManifest.StorageKey ?? item.Key);
                    JToken additionalDataJson = new JObject();
                    var json = dataItem?.Data;
                    if (json != null)
                    {
                        //if (LocaleController.Instance.GetLocales(_portalId).Count > 1)
                        {
                            JsonUtils.SimplifyJson(json, GetCurrentCultureCode());
                        }
                        additionalDataJson = json;
                    }
                    if (OpenContentControllerFactory.Instance.OpenContentGlobalSettingsController(_portalId).GetFastHandlebars())
                        _additionalData[(item.Value.ModelKey ?? item.Key)] = additionalDataJson;
                    else
                        _additionalData[(item.Value.ModelKey ?? item.Key).ToLowerInvariant()] = additionalDataJson;

                }
            }
            return _additionalData;
        }

        protected void ExtendSchemaOptions(JObject model, bool onlyData)
        {
            if (_portalSettings == null) onlyData = true;

            if (_templateFiles != null)
            {
                bool includeSchema = !onlyData && _templateFiles.SchemaInTemplate;
                bool includeOptions = _templateFiles.OptionsInTemplate;
                if (includeSchema || includeOptions)
                {
                    var alpaca = _ds.GetAlpaca(_dsContext, includeSchema, includeOptions, false);
                    // include SCHEMA info in the Model
                    if (includeSchema)
                    {
                        model["Schema"] = alpaca["schema"];
                        _schemaJson = alpaca["schema"] as JObject; // cache
                    }
                    // include OPTIONS info in the Model
                    if (includeOptions)
                    {
                        model["Options"] = alpaca["options"];
                        _optionsJson = alpaca["options"] as JObject; // cache
                    }
                }
            }
        }

        protected bool IsEditAllowed(int createdByUser)
        {
            string editRole = _manifest.GetEditRole();
            return (IsEditMode || OpenContentUtils.HasEditRole(_portalSettings, editRole, createdByUser)) // edit Role can edit whtout be in edit mode
                    && OpenContentUtils.HasEditPermissions(_portalSettings, _module.ViewModule, editRole, createdByUser);
        }

        protected bool HasEditPermissions(int createdByUser)
        {
            string editRole = _manifest.GetEditRole();
            return OpenContentUtils.HasEditPermissions(_portalSettings, _module.ViewModule, editRole, createdByUser);
        }

        protected string GetCurrentCultureCode()
        {
            if (string.IsNullOrEmpty(_cultureCode))
            {
                return DnnLanguageUtils.GetCurrentCultureCode();
            }
            else
            {
                return _cultureCode;
            }
        }

        private bool? _isEditMode;

        protected bool IsEditMode
        {
            get
            {
                //Perform tri-state switch check to avoid having to perform a security
                //role lookup on every property access (instead caching the result)
                if (!_isEditMode.HasValue)
                {
                    _isEditMode = _module.DataModule.CheckIfEditable(PortalSettings.Current);
                }
                return _isEditMode.Value;
            }
        }

        protected void LookupSelect2InOtherModule(JObject model, JObject options, bool onlyData)
        {

            foreach (var child in model.Children<JProperty>().ToList())
            {
                JObject opt = null;
                if (options?["fields"] != null)
                {
                    opt = options["fields"][child.Name] as JObject;
                }
                if (opt == null) continue;
                bool lookup =
                    opt["type"] != null &&
                    opt["type"].ToString() == "select2" &&
                    opt["dataService"]?["action"] != null &&
                    opt["dataService"]?["action"].ToString() == "Lookup";
 
                    //opt["dataService"]?["data"]?["moduleId"] != null &&
                    //opt["dataService"]?["data"]?["tabId"] != null;

                string dataMember = "";
                string valueField = "Id";
                string moduleId = "";
                string tabId = "";
                if (lookup)
                {
                    dataMember = opt["dataService"]["data"]["dataMember"]?.ToString() ?? "";
                    valueField = opt["dataService"]["data"]["valueField"]?.ToString() ?? "Id";
                    moduleId = opt["dataService"]["data"]["moduleId"]?.ToString() ?? "0";
                    tabId = opt["dataService"]["data"]["tabId"]?.ToString() ?? "0";
                }

                var childProperty = child;

                if (childProperty.Value is JArray)
                {
                    var array = childProperty.Value as JArray;
                    JArray newArray = new JArray();
                    foreach (var value in array)
                    {
                        var obj = value as JObject;
                        if (obj != null)
                        {
                            LookupSelect2InOtherModule(obj, opt["items"] as JObject, onlyData);
                        }
                        else if (lookup)
                        {
                            var val = value as JValue;
                            if (val != null)
                            {
                                try
                                {
                                    newArray.Add(GenerateObject(val.ToString(), int.Parse(tabId), int.Parse(moduleId), onlyData));
                                }
                                catch (System.Exception)
                                {
                                    Debugger.Break();
                                }
                            }
                        }
                    }
                    if (lookup)
                    {
                        childProperty.Value = newArray;
                    }
                }
                else if (childProperty.Value is JObject)
                {
                    var obj = childProperty.Value as JObject;
                    LookupSelect2InOtherModule(obj, opt, onlyData);
                }
                else if (childProperty.Value is JValue)
                {
                    if (lookup)
                    {
                        string val = childProperty.Value.ToString();
                        try
                        {                            
                            model[childProperty.Name] = GenerateObject(val, int.Parse(tabId), int.Parse(moduleId), onlyData);
                        }
                        catch (System.Exception ex)
                        {
                            Debugger.Break();
                        }
                    }
                }
            }
        }

        private JToken GenerateObject(string id, int tabId, int moduleId, bool onlyData)
        {
            var module = moduleId> 0 ? new OpenContentModuleInfo(moduleId , tabId) : _module;
            var ds = DataSourceManager.GetDataSource(module.Settings.Manifest.DataSource);
            var dsContext = OpenContentUtils.CreateDataContext(module);
            IDataItem dataItem = ds.Get(dsContext, id);
            if (dataItem != null)
            {
                var json = dataItem?.Data?.DeepClone() as JObject;
                //if (!string.IsNullOrEmpty(dataMember))
                //{
                //    json = json[dataMember];
                //}
                if (json != null)
                {
                    JsonUtils.SimplifyJson(json, GetCurrentCultureCode());
                    if (!onlyData)
                    {
                        var context = new JObject();
                        json["Context"] = context;
                        context["Id"] = dataItem.Id;
                        context["DetailUrl"] = GenerateDetailUrl(dataItem, json, module.Settings.Manifest, tabId > 0 ? tabId : _detailTabId);
                    }
                    return json;
                }
            }
            JObject res = new JObject();
            res["Id"] = id;
            res["Title"] = "unknow";
            return res;
        }

        protected string GenerateDetailUrl(IDataItem item, JObject dyn, Manifest.Manifest manifest, int detailTabId)
        {
            string url = "";
            if (!string.IsNullOrEmpty(manifest.DetailUrl))
            {
                HandlebarsEngine hbEngine = new HandlebarsEngine();
                var dynForHBS = JsonUtils.JsonToDictionary(dyn.ToString());

                url = hbEngine.Execute(manifest.DetailUrl, dynForHBS);
                url = HttpUtility.HtmlDecode(url);
            }
            return Globals.NavigateURL(detailTabId, false, _portalSettings, "", GetCurrentCultureCode(), UrlHelpers.CleanupUrl(url), "id=" + item.Id);
        }
    }
}