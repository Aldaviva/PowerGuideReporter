﻿using System;
using System.Web;
using DadsEnergyReporter.Data;
using DadsEnergyReporter.Injection;
using MimeKit;
using NodaTime;
using NodaTime.Text;

namespace DadsEnergyReporter.Service {

    public interface ReportFormatter {

        MimeMessage formatReport(SolarAndUtilityReport report);

    }

    [Component]
    internal class ReportFormatterImpl: ReportFormatter {

        private static readonly LocalDatePattern SHORT_DATE_PATTERN = LocalDatePattern.CreateWithCurrentCulture("M/d");

        public MimeMessage formatReport(SolarAndUtilityReport report) {
            var viewModel = new ReportViewModel(report);
            return new MimeMessage {
                Subject = formatSubject(report),
                Body = new BodyBuilder {
                    TextBody = formatBodyPlainText(viewModel),
                    HtmlBody = formatBodyHtml(viewModel)
                }.ToMessageBody()
            };
        }

        private struct ReportViewModel {

            private readonly SolarAndUtilityReport report;

            public ReportViewModel(SolarAndUtilityReport report) {
                this.report = report;
            }

            private double powerGenerated => report.powerGenerated;
            private int powerBoughtOrSold => report.powerBoughtOrSold;
            private bool didPurchase => report.powerBoughtOrSold >= 0;

            public string generatedLabel => "Generated by solar panels";
            public string generatedValue => $"{powerGenerated:N0} kWh";

            public string purchasedOrSoldLabel => $"{(didPurchase ? "Purchased from" : "Sold to")} O&R";
            public string purchasedOrSoldAmount => $"{Math.Abs(powerBoughtOrSold):N0} kWh";
            public string purchasedOrSoldCost => didPurchase ? $" for {report.powerCostCents / 100.0:C}" : "";

            public string totalLabel => "Total energy consumed";
            public string totalValue => $"{report.powerGenerated + report.powerBoughtOrSold:N0} kWh";

        }

        private static string formatSubject(SolarAndUtilityReport report) {
            return $"Electricity Usage Report for {shortDate(report.billingInterval.Start)}–" +
                   shortDate(report.billingInterval.End);
        }

        private static string formatBodyPlainText(ReportViewModel model) {
            return $@"{model.generatedLabel}: {model.generatedValue}
{model.purchasedOrSoldLabel}: {model.purchasedOrSoldAmount}{model.purchasedOrSoldCost}
---
{model.totalLabel}: {model.totalValue}";
        }

        private static string formatBodyHtml(ReportViewModel model) {
            return $@"<table style=""border-collapse: collapse"">
    <tr><td><b>{model.generatedLabel}:</b></td><td style=""text-align: right"">{model.generatedValue}</td><td></td></tr>
    <tr><td><b>{HttpUtility.HtmlEncode(model.purchasedOrSoldLabel)}:</b></td><td style=""text-align: right"">{model.purchasedOrSoldAmount}</td><td>{model.purchasedOrSoldCost}</td></tr>
    <tr style=""border-top: 1px solid black""><td><b>{model.totalLabel}:</b></td><td style=""text-align: right"">{model.totalValue}</td><td></td></tr>
</table>";
        }

        private static string shortDate(LocalDate date) {
            return SHORT_DATE_PATTERN.Format(date);
        }

    }

}