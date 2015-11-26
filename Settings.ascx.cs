#region Copyright

// 
// Copyright (c) 2015
// by Satrabel
// 

#endregion

#region Using Statements

using System;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Framework;
using Satrabel.OpenContent.Components;

#endregion

namespace Satrabel.OpenContent
{
    public partial class Settings : ModuleSettingsBase
    {
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            ServicesFramework.Instance.RequestAjaxScriptSupport();
            ServicesFramework.Instance.RequestAjaxAntiForgerySupport();
            //JavaScript.RequestRegistration(CommonJs.DnnPlugins); ;
            //JavaScript.RequestRegistration(CommonJs.jQueryFileUpload);
        }
        public override void LoadSettings()
        {
            FileUri template = new OpenContentSettings(Settings).Template.Uri;
            scriptList.Items.AddRange(OpenContentUtils.GetTemplatesFiles(PortalSettings, ModuleId, template, "OpenContent").ToArray());
            base.LoadSettings();
        }
        public override void UpdateSettings()
        {
            ModuleController mc = new ModuleController();
            mc.UpdateModuleSetting(ModuleId, "template", scriptList.SelectedValue);
            mc.UpdateModuleSetting(ModuleId, "data", HiddenField.Value);
        }
    }
}