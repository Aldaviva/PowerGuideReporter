﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DadsEnergyReporter.Data;
using DadsEnergyReporter.Exceptions;
using DadsEnergyReporter.Injection;
using DadsEnergyReporter.Properties;
using MailKit;
using MailKit.Security;
using MimeKit;

namespace DadsEnergyReporter.Service
{
    public interface EmailSender
    {
        Task SendEmail(Report report, IEnumerable<string> recipients);
    }

    [Component]
    internal class EmailSenderImpl : EmailSender
    {
        private readonly IMailTransport smtpClient;
        private readonly Settings settings = Settings.Default;
        private readonly ReportFormatter reportFormatter;

        public EmailSenderImpl(IMailTransport smtpClient, ReportFormatter reportFormatter)
        {
            this.smtpClient = smtpClient;
            this.reportFormatter = reportFormatter;
        }

        public async Task SendEmail(Report report, IEnumerable<string> recipients)
        {
            MimeMessage message = reportFormatter.FormatReport(report);
            message.From.Add(new MailboxAddress("Dad's Energy Reporter", settings.reportSenderEmail));
            message.To.AddRange(recipients.Select(recipient => new MailboxAddress(recipient)));

            try
            {
                await smtpClient.ConnectAsync(settings.smtpHost, settings.smtpPort, SecureSocketOptions.StartTls);
                await smtpClient.AuthenticateAsync(settings.smtpUsername, settings.smtpPassword);
                await smtpClient.SendAsync(message);
                await smtpClient.DisconnectAsync(true);
            }
            catch (IOException e)
            {
                throw new EmailException("Failed to send email message", e);
            }
        }
    }
}