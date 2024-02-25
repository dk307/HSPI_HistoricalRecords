using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using HomeSeer.PluginSdk;
using HomeSeer.PluginSdk.Devices;
using HomeSeer.PluginSdk.Devices.Controls;
using HomeSeer.PluginSdk.Devices.Identification;
using HomeSeer.PluginSdk.Energy;
using HomeSeer.PluginSdk.Events;
using HomeSeer.PluginSdk.Logging;
using Hspi;
using NUnit.Framework;
using Serilog.Events;

namespace HSPI_HistoryTest
{
    internal class FakeHSController : IHsController
    {
        public string DBPath => Path.Combine((this as IHsController).GetAppPath(), "data", PlugInData.PlugInId, "records.db");

        public FakeHSController()
        {
            // log level to verbose for tests
            SetupIniValue("Settings", SettingsPages.LoggingLevelId, ((int)LogEventLevel.Verbose).ToString());
        }

        double IHsController.APIVersion => 4;

        int IHsController.DeviceCount => throw new NotImplementedException();

        int IHsController.EventCount => throw new NotImplementedException();

        DateTime IHsController.SolarNoon => throw new NotImplementedException();

        DateTime IHsController.Sunrise => throw new NotImplementedException();

        DateTime IHsController.Sunset => throw new NotImplementedException();

        public IReadOnlyDictionary<int, NewDeviceData> CreatedDevices => createdDevices;

        public IReadOnlyDictionary<int, NewFeatureData> CreatedFeatures => createdFeatures;

        void IHsController.AddActionRunScript(int @ref, string script, string method, string parms) => throw new NotImplementedException();

        string IHsController.AddDeviceActionToEvent(int evRef, ControlEvent CC)
        {
            throw new NotImplementedException();
        }

        List<int> IHsController.AddRefsToCategory(string id, List<int> devRefs)
        {
            throw new NotImplementedException();
        }

        List<int> IHsController.AddRefToCategory(string id, int devRef)
        {
            throw new NotImplementedException();
        }

        void IHsController.AddStatusControlToFeature(int featRef, StatusControl statusControl)
        {
            throw new NotImplementedException();
        }

        void IHsController.AddStatusGraphicToFeature(int featRef, StatusGraphic statusGraphic)
        {
            throw new NotImplementedException();
        }

        int IHsController.CheckRegistrationStatus(string pluginId)
        {
            throw new NotImplementedException();
        }

        void IHsController.ClearIniSection(string sectionName, string fileName)
        {
            if (fileName != PlugInData.SettingFileName)
            {
                throw new NotImplementedException();
            }

            iniFile.TryRemove(sectionName, out var _);
        }

        void IHsController.ClearStatusControlsByRef(int featRef)
        {
            throw new NotImplementedException();
        }

        void IHsController.ClearStatusGraphicsByRef(int featRef)
        {
            throw new NotImplementedException();
        }

        string IHsController.CreateCategory(string name, string image)
        {
            throw new NotImplementedException();
        }

        int IHsController.CreateDevice(NewDeviceData deviceData)
        {
            int refId = newRefId++;
            createdDevices[refId] = deviceData;
            deviceOrFeatureData[refId] = new ConcurrentDictionary<EProperty, object>(deviceData.Device);
            return refId;
        }

        int IHsController.CreateEventWithNameInGroup(string name, string group)
        {
            throw new NotImplementedException();
        }

        int IHsController.CreateFeatureForDevice(NewFeatureData featureData)
        {
            int refId = newRefId++;
            createdFeatures[refId] = featureData;
            deviceOrFeatureData[refId] = new ConcurrentDictionary<EProperty, object>(featureData.Feature);

            AddParentAssociation(featureData, refId);

            return refId;

            void AddParentAssociation(NewFeatureData featureData, int refId)
            {
                var parentRefId = ((HashSet<int>)featureData.Feature[EProperty.AssociatedDevices]).First();

                HashSet<int> parentAssociation;

                if (deviceOrFeatureData[parentRefId].TryGetValue(EProperty.AssociatedDevices, out var parentAssociationTemp))
                {
                    parentAssociation = (HashSet<int>)parentAssociationTemp;
                }
                else
                {
                    parentAssociation = new HashSet<int>();
                }

                parentAssociation.Add(refId);

                SetupDevOrFeatureValue(parentRefId, EProperty.AssociatedDevices, parentAssociation);
            }
        }

        string IHsController.CreateVar(string name)
        {
            throw new NotImplementedException();
        }

        string IHsController.DecryptString(string text, string password, string keyModifier)
        {
            throw new NotImplementedException();
        }

        void IHsController.DeleteAfterTrigger_Clear(int evRef)
        {
            throw new NotImplementedException();
        }

        void IHsController.DeleteAfterTrigger_Set(int evRef)
        {
            throw new NotImplementedException();
        }

        void IHsController.DeleteCategoryById(string id)
        {
            throw new NotImplementedException();
        }

        bool IHsController.DeleteDevice(int devRef)
        {
            return deviceOrFeatureData.TryRemove(devRef, out var _);
        }

        bool IHsController.DeleteDevicesByInterface(string interfaceName)
        {
            throw new NotImplementedException();
        }

        void IHsController.DeleteEventByRef(int evRef)
        {
            throw new NotImplementedException();
        }

        bool IHsController.DeleteFeature(int featRef)
        {
            return deviceOrFeatureData.TryRemove(featRef, out var _);
        }

        bool IHsController.DeleteImageFile(string targetFile)
        {
            throw new NotImplementedException();
        }

        bool IHsController.DeleteStatusControlByValue(int featRef, double value)
        {
            throw new NotImplementedException();
        }

        bool IHsController.DeleteStatusGraphicByValue(int featRef, double value)
        {
            throw new NotImplementedException();
        }

        void IHsController.DeleteVar(string name)
        {
            throw new NotImplementedException();
        }

        void IHsController.DisableEventByRef(int evRef)
        {
            throw new NotImplementedException();
        }

        bool IHsController.DoesRefExist(int devOrFeatRef)
        {
            throw new NotImplementedException();
        }

        void IHsController.EnableEventByRef(int evref)
        {
            throw new NotImplementedException();
        }

        string IHsController.Energy_AddCalculator(int dvRef, string Name, TimeSpan Range, TimeSpan StartBack)
        {
            throw new NotImplementedException();
        }

        string IHsController.Energy_AddCalculatorEvenDay(int dvRef, string Name, TimeSpan Range, TimeSpan StartBack)
        {
            throw new NotImplementedException();
        }

        bool IHsController.Energy_AddData(int dvRef, EnergyData Data)
        {
            throw new NotImplementedException();
        }

        bool IHsController.Energy_AddDataArray(int dvRef, EnergyData[] colData)
        {
            throw new NotImplementedException();
        }

        int IHsController.Energy_CalcCount(int dvRef)
        {
            throw new NotImplementedException();
        }

        List<EnergyData> IHsController.Energy_GetArchiveData(int dvRef, DateTime dteStart, DateTime dteEnd)
        {
            throw new NotImplementedException();
        }

        List<EnergyData> IHsController.Energy_GetArchiveDatas(string dvRefs, DateTime dteStart, DateTime dteEnd)
        {
            throw new NotImplementedException();
        }

        EnergyCalcData IHsController.Energy_GetCalcByIndex(int dvRef, int Index)
        {
            throw new NotImplementedException();
        }

        EnergyCalcData IHsController.Energy_GetCalcByName(int dvRef, string Name)
        {
            throw new NotImplementedException();
        }

        List<EnergyData> IHsController.Energy_GetData(int dvRef, DateTime dteStart, DateTime dteEnd)
        {
            throw new NotImplementedException();
        }

        SortedList<int, string> IHsController.Energy_GetEnergyRefs(bool GetParentRefs)
        {
            throw new NotImplementedException();
        }

        Image IHsController.Energy_GetGraph(int id, string dvRefs, int width, int height, string format)
        {
            throw new NotImplementedException();
        }

        EnergyGraphData IHsController.Energy_GetGraphData(int ID)
        {
            throw new NotImplementedException();
        }

        SortedList<int, string> IHsController.Energy_GetGraphDataIDs()
        {
            throw new NotImplementedException();
        }

        int IHsController.Energy_RemoveData(int dvRef, DateTime dteStart)
        {
            throw new NotImplementedException();
        }

        int IHsController.Energy_SaveGraphData(EnergyGraphData Data)
        {
            throw new NotImplementedException();
        }

        bool IHsController.Energy_SetEnergyDevice(int dvRef, Constants.enumEnergyDevice DeviceType)
        {
            throw new NotImplementedException();
        }

        bool IHsController.EventEnabled(int evRef)
        {
            throw new NotImplementedException();
        }

        bool IHsController.EventExistsByRef(int evRef)
        {
            throw new NotImplementedException();
        }

        bool IHsController.EventSetRecurringTrigger(int evRef, TimeSpan Frequency, bool Once_Per_Hour, bool Reference_To_Hour)
        {
            throw new NotImplementedException();
        }

        bool IHsController.EventSetTimeTrigger(int evRef, DateTime DT)
        {
            throw new NotImplementedException();
        }

        List<TrigActInfo> IHsController.GetActionsByInterface(string pluginId)
        {
            throw new NotImplementedException();
        }

        Dictionary<string, string> IHsController.GetAllCategories()
        {
            throw new NotImplementedException();
        }

        List<int> IHsController.GetAllDeviceRefs()
        {
            throw new NotImplementedException();
        }

        List<HsDevice> IHsController.GetAllDevices(bool withFeatures)
        {
            throw new NotImplementedException();
        }

        List<EventGroupData> IHsController.GetAllEventGroups()
        {
            throw new NotImplementedException();
        }

        List<EventData> IHsController.GetAllEvents()
        {
            throw new NotImplementedException();
        }

        List<int> IHsController.GetAllFeatureRefs()
        {
            throw new NotImplementedException();
        }

        List<int> IHsController.GetAllRefs() => deviceOrFeatureData.Keys.ToList();

        string IHsController.GetAppPath() => appPath;

        string IHsController.GetCategoryImageById(string id)
        {
            throw new NotImplementedException();
        }

        string IHsController.GetCategoryNameById(string id)
        {
            throw new NotImplementedException();
        }

        HsDevice IHsController.GetDeviceByAddress(string devAddress)
        {
            throw new NotImplementedException();
        }

        HsDevice IHsController.GetDeviceByCode(string devCode)
        {
            throw new NotImplementedException();
        }

        HsDevice IHsController.GetDeviceByRef(int devRef)
        {
            throw new NotImplementedException();
        }

        HsDevice IHsController.GetDeviceWithFeaturesByRef(int devRef)
        {
            throw new NotImplementedException();
        }

        EventData IHsController.GetEventByRef(int eventRef)
        {
            throw new NotImplementedException();
        }

        EventGroupData IHsController.GetEventGroupById(int groupRef)
        {
            throw new NotImplementedException();
        }

        string IHsController.GetEventNameByRef(int eventRef)
        {
            throw new NotImplementedException();
        }

        int IHsController.GetEventRefByName(string eventName)
        {
            throw new NotImplementedException();
        }

        int IHsController.GetEventRefByNameAndGroup(string eventName, string eventGroup)
        {
            throw new NotImplementedException();
        }

        List<EventData> IHsController.GetEventsByGroup(int groupId)
        {
            throw new NotImplementedException();
        }

        DateTime IHsController.GetEventTriggerTime(int evRef)
        {
            throw new NotImplementedException();
        }

        string IHsController.GetEventVoiceCommand(int evRef)
        {
            throw new NotImplementedException();
        }

        public HsFeature GetFeature(int devOrFeatRef)
        {
            if (deviceOrFeatureData.TryGetValue(devOrFeatRef, out var featureData))
            {
                HsFeature hsFeature = new(devOrFeatRef);

                foreach (var data in featureData)
                {
                    hsFeature.Changes[data.Key] = data.Value;
                }

                return hsFeature;
            }

            Assert.Fail($"{devOrFeatRef} feature not setup");
            return null;
        }

        HsFeature IHsController.GetFeatureByAddress(string featAddress)
        {
            throw new NotImplementedException();
        }

        HsFeature IHsController.GetFeatureByCode(string featCode)
        {
            throw new NotImplementedException();
        }

        HsFeature IHsController.GetFeatureByRef(int featRef)
        {
            return GetFeature(featRef);
        }

        List<string> IHsController.GetFirstLocationList()
        {
            throw new NotImplementedException();
        }

        string IHsController.GetFirstLocationName()
        {
            throw new NotImplementedException();
        }

        Constants.editions IHsController.GetHSEdition()
        {
            throw new NotImplementedException();
        }

        Dictionary<string, string> IHsController.GetIniSection(string section, string fileName)
        {
            if (fileName != PlugInData.SettingFileName)
            {
                throw new NotImplementedException();
            }

            if (iniFile.TryGetValue(section, out var iniSection))
            {
                return iniSection.ToDictionary(x => x.Key, x => x.Value);
            }

            return new Dictionary<string, string>();
        }

        string IHsController.GetINISetting(string sectionName, string key, string defaultVal, string fileName)
        {
            if (iniFile.TryGetValue(sectionName, out var iniValues) &&
                iniValues.TryGetValue(key, out var iniValue))
            {
                return iniValue;
            }

            return defaultVal;
        }

        string IHsController.GetIpAddress()
        {
            throw new NotImplementedException();
        }

        string IHsController.GetLocation1Name()
        {
            throw new NotImplementedException();
        }

        string IHsController.GetLocation2Name()
        {
            throw new NotImplementedException();
        }

        SortedList IHsController.GetLocations2List()
        {
            throw new NotImplementedException();
        }

        SortedList IHsController.GetLocationsList()
        {
            throw new NotImplementedException();
        }

        string IHsController.GetNameByRef(int devOrFeatRef)
        {
            return (string)GetFeatureValue(devOrFeatRef, EProperty.Name);
        }

        int IHsController.GetOsType()
        {
            throw new NotImplementedException();
        }

        string IHsController.GetPluginVersionById(string pluginId)
        {
            throw new NotImplementedException();
        }

        string IHsController.GetPluginVersionByName(string pluginName)
        {
            throw new NotImplementedException();
        }

        Dictionary<int, object> IHsController.GetPropertyByInterface(string interfaceName, EProperty property, bool deviceOnly)
        {
            throw new NotImplementedException();
        }

        object IHsController.GetPropertyByRef(int devOrFeatRef, EProperty property)
        {
            if (deviceOrFeatureData.TryGetValue(devOrFeatRef, out var keyValues) &&
                keyValues.TryGetValue(property, out var value))
            {
                if (property == EProperty.StatusGraphics)
                {
                    if (value is StatusGraphicCollection collection)
                    {
                        return collection.Values;
                    }
                }
                return value;
            }

            throw new Exception($"Property {property} not set for {devOrFeatRef}");
        }

        List<int> IHsController.GetRefsByCategoryId(string id)
        {
            throw new NotImplementedException();
        }

        List<int> IHsController.GetRefsByInterface(string interfaceName, bool deviceOnly)
        {
            return deviceOrFeatureData.Where(x =>
            {
                if (x.Value.TryGetValue(EProperty.Interface, out var interfaceForEntry) &&
                    interfaceName == (string)interfaceForEntry)
                {
                    if (deviceOnly && x.Value.TryGetValue(EProperty.Relationship, out var rel))
                    {
                        return (ERelationship)rel == ERelationship.Device;
                    }

                    return true;
                }

                return false;
            }).Select(x => x.Key).ToList();
        }

        List<string> IHsController.GetSecondLocationList()
        {
            throw new NotImplementedException();
        }

        string IHsController.GetSecondLocationName()
        {
            throw new NotImplementedException();
        }

        string IHsController.GetSpeakerInstanceList()
        {
            throw new NotImplementedException();
        }

        StatusControlCollection IHsController.GetStatusControlCollectionByRef(int featRef)
        {
            throw new NotImplementedException();
        }

        int IHsController.GetStatusControlCountByRef(int featRef)
        {
            throw new NotImplementedException();
        }

        StatusControl IHsController.GetStatusControlForLabel(int featRef, string label)
        {
            throw new NotImplementedException();
        }

        StatusControl IHsController.GetStatusControlForValue(int featRef, double value)
        {
            throw new NotImplementedException();
        }

        List<StatusControl> IHsController.GetStatusControlsByRef(int featRef)
        {
            throw new NotImplementedException();
        }

        List<StatusControl> IHsController.GetStatusControlsForRange(int featRef, double min, double max)
        {
            throw new NotImplementedException();
        }

        int IHsController.GetStatusGraphicCountByRef(int featRef)
        {
            throw new NotImplementedException();
        }

        StatusGraphic IHsController.GetStatusGraphicForValue(int featRef, double value)
        {
            throw new NotImplementedException();
        }

        List<StatusGraphic> IHsController.GetStatusGraphicsByRef(int featRef)
        {
            throw new NotImplementedException();
        }

        List<StatusGraphic> IHsController.GetStatusGraphicsForRange(int featRef, double min, double max)
        {
            throw new NotImplementedException();
        }

        TrigActInfo[] IHsController.GetTriggersByInterface(string pluginId)
        {
            throw new NotImplementedException();
        }

        TrigActInfo[] IHsController.GetTriggersByType(string pluginId, int trigId)
        {
            throw new NotImplementedException();
        }

        string IHsController.GetUsers()
        {
            throw new NotImplementedException();
        }

        object IHsController.GetVar(string name)
        {
            throw new NotImplementedException();
        }

        int IHsController.GetVolume(string host)
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsEventLoggingEnabledByRef(int eventRef)
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsFeatureValueValid(int featRef)
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsFlagOnRef(int devOrFeatRef, EMiscFlag miscFlag)
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsLicensed()
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsLocation1First()
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsMediaPlaying(string host)
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsRefDevice(int devOrFeatRef)
        {
            if (deviceOrFeatureData.TryGetValue(devOrFeatRef, out var result) &&
                result.TryGetValue(EProperty.Relationship, out var relationship))
            {
                return (ERelationship)relationship == ERelationship.Device;
            }

            return false;
        }

        bool IHsController.IsRegistered()
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsScriptRunning(string scr)
        {
            throw new NotImplementedException();
        }

        bool IHsController.IsSpeakerBusy(string host)
        {
            throw new NotImplementedException();
        }

        object IHsController.LegacyPluginFunction(string plugName, string plugInstance, string procName, object[] @params)
        {
            throw new NotImplementedException();
        }

        object IHsController.LegacyPluginPropertyGet(string plugName, string plugInstance, string propName)
        {
            throw new NotImplementedException();
        }

        void IHsController.LegacyPluginPropertySet(string plugName, string plugInstance, string propName, object propValue)
        {
            throw new NotImplementedException();
        }

        void IHsController.PlayWavFile(string FileName, string Host, bool Wait)
        {
            throw new NotImplementedException();
        }

        object IHsController.PluginFunction(string pluginId, string procName, object[] @params)
        {
            throw new NotImplementedException();
        }

        object IHsController.PluginPropertyGet(string plugId, string propName)
        {
            throw new NotImplementedException();
        }

        void IHsController.PluginPropertySet(string plugId, string propName, object propValue)
        {
            throw new NotImplementedException();
        }

        void IHsController.RaiseGenericEventCB(string genericType, object[] parms, string pluginId)
        {
            if (pluginId != PlugInData.PlugInId)
            {
                throw new NotImplementedException();
            }
        }

        void IHsController.RegisterDeviceIncPage(string pluginId, string pageFilename, string linkText)
        {
            if (pluginId != PlugInData.PlugInId)
            {
                throw new NotImplementedException();
            }
        }

        void IHsController.RegisterEventCB(Constants.HSEvent evType, string pluginId)
        {
            if (pluginId != PlugInData.PlugInId)
            {
                throw new NotImplementedException();
            }
        }

        void IHsController.RegisterFeaturePage(string pluginId, string pageFilename, string linkText)
        {
            if (pluginId != PlugInData.PlugInId)
            {
                throw new NotImplementedException();
            }
        }

        void IHsController.RegisterGenericEventCB(string genericType, string pluginId)
        {
            if (pluginId != PlugInData.PlugInId)
            {
                throw new NotImplementedException();
            }
        }

        bool IHsController.RegisterPlugin(string pluginId, string pluginName)
        {
            if (pluginId != PlugInData.PlugInId)
            {
                throw new NotImplementedException();
            }

            return true;
        }

        void IHsController.RegisterProxySpeakPlug(string pluginId)
        {
            throw new NotImplementedException();
        }

        List<int> IHsController.RemoveRefFromCategory(string id, int devRef)
        {
            throw new NotImplementedException();
        }

        List<int> IHsController.RemoveRefsFromCategory(string id, List<int> devRefs)
        {
            throw new NotImplementedException();
        }

        string IHsController.ReplaceVariables(string strIn)
        {
            throw new NotImplementedException();
        }

        void IHsController.RestartSystem()
        {
            throw new NotImplementedException();
        }

        object IHsController.RunScript(string scr, bool Wait, bool SingleInstance)
        {
            throw new NotImplementedException();
        }

        object IHsController.RunScriptFunc(string scr, string func, object param, bool Wait, bool SingleInstance)
        {
            throw new NotImplementedException();
        }

        bool IHsController.SaveImageFile(byte[] imageBytes, string destinationFile, bool overwriteExistingFile)
        {
            throw new NotImplementedException();
        }

        void IHsController.SaveINISetting(string sectionName, string key, string value, string fileName)
        {
            if (fileName != PlugInData.SettingFileName)
            {
                throw new NotImplementedException();
            }

            SetupIniValue(sectionName, key, value);
        }

        string IHsController.SaveVar(string name, object obj)
        {
            throw new NotImplementedException();
        }

        bool IHsController.SendControlForFeatureByString(int devOrFeatRef, double controlValue, string controlString)
        {
            throw new NotImplementedException();
        }

        bool IHsController.SendControlForFeatureByValue(int devOrFeatRef, double value)
        {
            throw new NotImplementedException();
        }

        void IHsController.SendEmail(string to, string from, string cc, string bcc, string subject, string message, string attach)
        {
            throw new NotImplementedException();
        }

        void IHsController.SetImageForCategoryById(string id, string image)
        {
            throw new NotImplementedException();
        }

        public void SetupIniSettingsSection(string sectionName, Dictionary<string, string> settingsFromIni)
        {
            iniFile[sectionName] = new ConcurrentDictionary<string, string>(settingsFromIni);
        }

        void IHsController.SetNameForCategoryById(string id, string name)
        {
            throw new NotImplementedException();
        }

        void IHsController.SetRefsForCategoryById(string id, List<int> devRefs)
        {
            throw new NotImplementedException();
        }

        public void SetupDevice(int deviceRefId,
                                 double value = 0,
                                 string displayString = null,
                                 DateTime? lastChange = null,
                                 string deviceInterface = null)
        {
            var properties = new ConcurrentDictionary<EProperty, object>();
            properties[EProperty.Ref] = deviceRefId;
            properties[EProperty.DeviceType] = new HomeSeer.PluginSdk.Devices.Identification.TypeInfo() { ApiType = EApiType.Device };
            properties[EProperty.Relationship] = ERelationship.Device;
            properties[EProperty.StatusGraphics] = new List<StatusGraphic>();
            properties[EProperty.PlugExtraData] = new PlugExtraData();
            properties[EProperty.Interface] = deviceInterface ?? "Z-Wave";
            properties[EProperty.Value] = value;
            properties[EProperty.DisplayedStatus] = displayString;
            properties[EProperty.LastChange] = lastChange ?? new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Local);

            deviceOrFeatureData[deviceRefId] = properties;
        }

        public object GetFeatureValue(int devOrFeatRef, EProperty property)
        {
            if (deviceOrFeatureData.TryGetValue(devOrFeatRef, out var keyValues) &&
                keyValues.TryGetValue(property, out var value))
            {
                return value;
            }

            return null;
        }

        public DateTime GetFeatureLastChange(int devOrFeatRef)
        {
            return (DateTime)GetFeatureValue(devOrFeatRef, EProperty.LastChange);
        }

        public void SetupDevOrFeatureValue(int devOrFeatRef, EProperty property, object value)
        {
            if (deviceOrFeatureData.TryGetValue(devOrFeatRef, out var dict))
            {
                dict[property] = value;
            }
            else
            {
                throw new Exception($"{devOrFeatRef} not setup");
            }
        }

        public bool RemoveFeatureOrDevice(int devOrDeatRef)
        {
            return deviceOrFeatureData.TryRemove(devOrDeatRef, out var _);
        }

        public void SetupFeature(int deviceRefId,
                                 double value,
                                 string displayString = null,
                                 DateTime? lastChange = null,
                                 string featureInterface = null)
        {
            var properties = new ConcurrentDictionary<EProperty, object>();
            properties[EProperty.Ref] = deviceRefId;
            properties[EProperty.DeviceType] = new HomeSeer.PluginSdk.Devices.Identification.TypeInfo() { ApiType = EApiType.Feature };
            properties[EProperty.Relationship] = ERelationship.Feature;
            properties[EProperty.StatusGraphics] = new StatusGraphicCollection();
            properties[EProperty.PlugExtraData] = new PlugExtraData();
            properties[EProperty.Interface] = featureInterface ?? "Z-Wave";
            properties[EProperty.Value] = value;
            properties[EProperty.DisplayedStatus] = displayString;
            properties[EProperty.LastChange] = lastChange ?? new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Local);

            deviceOrFeatureData[deviceRefId] = properties;
        }

        public void SetupIniValue(string section, string key, string value)
        {
            if (iniFile.TryGetValue(section, out var dict))
            {
                dict[key] = value;
            }
            else
            {
                var dict2 = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                dict2[key] = value;
                iniFile[section] = dict2;
            }
        }

        void IHsController.SetVolume(int level, string Host)
        {
            throw new NotImplementedException();
        }

        void IHsController.ShutDown()
        {
            throw new NotImplementedException();
        }

        void IHsController.Speak(int speechDevice, string spokenText, bool wait, string host)
        {
            throw new NotImplementedException();
        }

        void IHsController.SpeakProxy(int speechDevice, string spokenText, bool wait, string host)
        {
            throw new NotImplementedException();
        }

        bool IHsController.SpeakToFile(string Text, string Voice, string FileName)
        {
            throw new NotImplementedException();
        }

        bool IHsController.TriggerEventByRef(int eventRef)
        {
            throw new NotImplementedException();
        }

        void IHsController.TriggerFire(string pluginId, TrigActInfo trigInfo)
        {
            throw new NotImplementedException();
        }

        TrigActInfo[] IHsController.TriggerMatches(string pluginId, int trigId, int subTrigId)
        {
            throw new NotImplementedException();
        }

        void IHsController.UnregisterDeviceIncPage(string pluginId)
        {
            throw new NotImplementedException();
        }

        void IHsController.UnregisterFeaturePage(string pluginId, string pageFilename)
        {
            throw new NotImplementedException();
        }

        void IHsController.UnRegisterGenericEventCB(string genericType, string pluginId)
        {
            throw new NotImplementedException();
        }

        void IHsController.UnRegisterProxySpeakPlug(string pluginId)
        {
            throw new NotImplementedException();
        }

        HsDevice IHsController.UpdateDeviceByRef(int devRef, Dictionary<EProperty, object> changes)
        {
            throw new NotImplementedException();
        }

        HsFeature IHsController.UpdateFeatureByRef(int featRef, Dictionary<EProperty, object> changes)
        {
            throw new NotImplementedException();
        }

        bool IHsController.UpdateFeatureValueByRef(int featRef, double value)
        {
            SetupDevOrFeatureValue(featRef, EProperty.Value, value);
            return true;
        }

        bool IHsController.UpdateFeatureValueStringByRef(int featRef, string value)
        {
            SetupDevOrFeatureValue(featRef, EProperty.DisplayedStatus, value);
            return true;
        }

        string IHsController.UpdatePlugAction(string plugId, int evRef, TrigActInfo actInfo)
        {
            throw new NotImplementedException();
        }

        string IHsController.UpdatePlugTrigger(string plugId, int evRef, TrigActInfo trigInfo)
        {
            throw new NotImplementedException();
        }

        void IHsController.UpdatePropertyByRef(int devOrFeatRef, EProperty property, object value)
        {
            SetupDevOrFeatureValue(devOrFeatRef, property, value);
        }

        string IHsController.Version()
        {
            throw new NotImplementedException();
        }

        void IHsController.WindowsLogoffSystem()
        {
            throw new NotImplementedException();
        }

        void IHsController.WindowsRebootSystem()
        {
            throw new NotImplementedException();
        }

        void IHsController.WindowsShutdownSystem()
        {
            throw new NotImplementedException();
        }

        void IHsController.WriteLog(ELogType logType, string message, string pluginName, string color)
        {
            if (pluginName != PlugInData.PlugInName)
            {
                throw new NotImplementedException();
            }
        }

        private int newRefId = 948504;
        private readonly ConcurrentDictionary<int, NewFeatureData> createdFeatures = new();
        private readonly ConcurrentDictionary<int, NewDeviceData> createdDevices = new();
        private readonly string appPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        private readonly ConcurrentDictionary<int, ConcurrentDictionary<EProperty, object>> deviceOrFeatureData = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> iniFile = new(StringComparer.OrdinalIgnoreCase);
    }
}