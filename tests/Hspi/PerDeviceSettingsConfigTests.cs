using System;
using System.Collections.Immutable;
using HomeSeer.PluginSdk;
using HSPI_HistoryTest;
using Moq;
using NUnit.Framework;

namespace Hspi.Tests
{
    [TestFixture]
    public class PerDeviceSettingsConfigTests
    {
        private const string FileName = "History.ini";
        private FakeHSController mockHsController;
        private PerDeviceSettingsConfig config;

        [SetUp]
        public void SetUp()
        {
            mockHsController = new FakeHSController();
            config = new PerDeviceSettingsConfig(mockHsController);
        }

        [Test]
        public void AddUpdateSingle()
        {
            var device = new PerDeviceSettings(133, true, TimeSpan.FromDays(1), 10, 20);
            config.AddOrUpdate(device);

            Assert.That(config.DeviceSettings.ContainsKey(device.DeviceRefId), Is.True);

            Assert.That(config.DeviceSettings[device.DeviceRefId].IsTracked, Is.EqualTo(device.IsTracked));
            Assert.That(config.DeviceSettings[device.DeviceRefId].MinValue, Is.EqualTo(device.MinValue));
            Assert.That(config.DeviceSettings[device.DeviceRefId].MaxValue, Is.EqualTo(device.MaxValue));

            var settings = (mockHsController as IHsController).GetIniSection("Settings", FileName);

            Assert.True(settings.TryGetValue("DeviceSettings", out var value));
            Assert.That(value, Is.EqualTo(device.DeviceRefId.ToString()));


            var deviceSettings = (mockHsController as IHsController).GetIniSection(device.DeviceRefId.ToString(), FileName);

            Assert.True(deviceSettings.TryGetValue("RefId", out var refId));
            Assert.That(refId, Is.EqualTo(device.DeviceRefId.ToString()));

            Assert.True(deviceSettings.TryGetValue("IsTracked", out var isTracked));
            Assert.That(isTracked, Is.EqualTo(device.IsTracked.ToString()));
            Assert.True(deviceSettings.TryGetValue("RetentionPeriod", out var retentionPeriod));
            Assert.That(retentionPeriod, Is.EqualTo(device.RetentionPeriod.ToString()));
        }

        [Test]
        public void AddUpdateMultiple()
        {
            var device1 = new PerDeviceSettings(133, true, TimeSpan.FromDays(1), 10, 20);
            var device2 = new PerDeviceSettings(134, false, TimeSpan.FromDays(1), 10, 20);
            config.AddOrUpdate(device1);
            config.AddOrUpdate(device2);


            var settings = (mockHsController as IHsController).GetIniSection("Settings", FileName);

            Assert.True(settings.TryGetValue("DeviceSettings", out var value));
            Assert.That(value, Is.EqualTo(device1.DeviceRefId.ToString() + "," + device2.DeviceRefId.ToString()));
        }


        [Test]
        public void AddUpdateAndRemove()
        {
            var device1 = new PerDeviceSettings(133, true, TimeSpan.FromDays(1), 10, 20);
            var device2 = new PerDeviceSettings(134, false, TimeSpan.FromDays(1), 10, 20);
            config.AddOrUpdate(device1);
            config.AddOrUpdate(device2);


            config.Remove(device1.DeviceRefId);


            var settings = (mockHsController as IHsController).GetIniSection("Settings", FileName);

            Assert.True(settings.TryGetValue("DeviceSettings", out var value));
            Assert.That(value, Is.EqualTo(device2.DeviceRefId.ToString()));
        }
    }
}
