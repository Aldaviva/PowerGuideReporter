﻿using System.Collections.Generic;
using DadsEnergyReporter.Data;
using DadsEnergyReporter.Properties;
using DadsEnergyReporter.Remote.OrangeRockland.Service;
using DadsEnergyReporter.Remote.PowerGuide.Service;
using FakeItEasy;
using FluentAssertions;
using NodaTime;
using Xunit;

namespace DadsEnergyReporter.Service
{
    public class EnergyReporterTest
    {
        private readonly EnergyReporterImpl energyReporter;
        private readonly ReportGenerator reportGenerator = A.Fake<ReportGenerator>();
        private readonly EmailSender emailSender = A.Fake<EmailSender>();
        private readonly PowerGuideAuthenticationService powerGuideAuthenticationService = A.Fake<PowerGuideAuthenticationService>();
        private readonly OrangeRocklandAuthenticationService orangeRocklandAuthenticationService = A.Fake<OrangeRocklandAuthenticationService>();

        private static readonly DateTimeZone ZONE = DateTimeZoneProviders.Tzdb["America/New_York"];

        public EnergyReporterTest()
        {
            energyReporter = new EnergyReporterImpl(reportGenerator, emailSender, powerGuideAuthenticationService, orangeRocklandAuthenticationService, ZONE);
        }

        [Fact]
        public async void Normal()
        {
            Settings settings = Settings.Default;
            settings.orangeRocklandUsername = "oruUser";
            settings.orangeRocklandPassword = "oruPass";
            settings.solarCityUsername = "solarcityUser";
            settings.solarCityPassword = "solarcityPass";
            settings.mostRecentReportBillingDate = 0;
            settings.reportRecipientEmails = new List<string> { "ben@aldaviva.com" };
            settings.reportSenderEmail = "dadsenergyreporter@aldaviva.com";

            var report = new Report(100, new DateInterval(new LocalDate(2017, 07, 17), new LocalDate(2017, 08, 16)));
            A.CallTo(() => reportGenerator.GenerateReport()).Returns(report);

            await energyReporter.Start();

            A.CallTo(() => powerGuideAuthenticationService.GetAuthToken()).MustHaveHappened();
            A.CallTo(() => orangeRocklandAuthenticationService.GetAuthToken()).MustHaveHappened();
            A.CallTo(() => reportGenerator.GenerateReport()).MustHaveHappened();
            A.CallTo(() => emailSender.SendEmail(report, A<IEnumerable<string>>.That.IsSameSequenceAs(new List<string> { "ben@aldaviva.com" }))).MustHaveHappened();

            A.CallTo(() => powerGuideAuthenticationService.LogOut()).MustHaveHappened();
            A.CallTo(() => orangeRocklandAuthenticationService.LogOut()).MustHaveHappened();

            settings.mostRecentReportBillingDate.Should().Be(report.BillingDate.AtStartOfDayInZone(ZONE).ToInstant().ToUnixTimeMilliseconds());
        }

        [Fact]
        public async void SkipsIfTooFewDaysSinceLastReport()
        {
            Settings settings = Settings.Default;
            settings.orangeRocklandUsername = "oruUser";
            settings.orangeRocklandPassword = "oruPass";
            settings.solarCityUsername = "solarcityUser";
            settings.solarCityPassword = "solarcityPass";
            settings.mostRecentReportBillingDate = SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromDays(27)).ToUnixTimeMilliseconds();
            settings.reportRecipientEmails = new List<string> { "ben@aldaviva.com" };
            settings.reportSenderEmail = "dadsenergyreporter@aldaviva.com";

            await energyReporter.Start();

            A.CallTo(() => powerGuideAuthenticationService.GetAuthToken()).MustNotHaveHappened();
            A.CallTo(() => orangeRocklandAuthenticationService.GetAuthToken()).MustNotHaveHappened();
            A.CallTo(() => reportGenerator.GenerateReport()).MustNotHaveHappened();
            A.CallTo(() => emailSender.SendEmail(A<Report>._, A<IEnumerable<string>>._)).MustNotHaveHappened();
        }
        
        [Fact]
        public async void SkipsIfAlreadySentReport()
        {
            Settings settings = Settings.Default;
            settings.orangeRocklandUsername = "oruUser";
            settings.orangeRocklandPassword = "oruPass";
            settings.solarCityUsername = "solarcityUser";
            settings.solarCityPassword = "solarcityPass";
            settings.mostRecentReportBillingDate = new LocalDate(2017, 08, 16).AtStartOfDayInZone(ZONE).ToInstant().ToUnixTimeMilliseconds();
            settings.reportRecipientEmails = new List<string> { "ben@aldaviva.com" };
            settings.reportSenderEmail = "dadsenergyreporter@aldaviva.com";

            var report = new Report(100, new DateInterval(new LocalDate(2017, 07, 17), new LocalDate(2017, 08, 16)));
            A.CallTo(() => reportGenerator.GenerateReport()).Returns(report);
            
            await energyReporter.Start();

            A.CallTo(() => powerGuideAuthenticationService.GetAuthToken()).MustHaveHappened();
            A.CallTo(() => orangeRocklandAuthenticationService.GetAuthToken()).MustHaveHappened();
            A.CallTo(() => reportGenerator.GenerateReport()).MustHaveHappened();
            
            A.CallTo(() => emailSender.SendEmail(A<Report>._, A<IEnumerable<string>>._)).MustNotHaveHappened();
        }
    }
}