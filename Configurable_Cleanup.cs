using System;
using System.IO;
using Microsoft.Extensions.Logging;
using MailKit.Net.Smtp;
using MimeKit;
using System.Net.Security;

namespace Configurable_Cleanup
{
    public class Configurable_Cleanup : BackgroundService
    {
        private readonly ILogger<Configurable_Cleanup> _logger;
        private readonly IConfiguration _configuration;
        private readonly string[] _directoryPaths;
        private readonly string[]? _fileExtensions;
        private readonly bool _dryMode;
        private readonly int _IntervalInSeconds;

        public Configurable_Cleanup(ILogger<Configurable_Cleanup> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            _directoryPaths = _configuration.GetSection("CleanupSettings:DirectoryPaths").Get<string[]>() ?? Array.Empty<string>();
            _fileExtensions = _configuration.GetSection("CleanupSettings:FileExtensions").Get<string[]>();
            _dryMode = _configuration.GetValue<bool>("CleanupSettings:DryMode", false);
            _IntervalInSeconds = _configuration.GetValue<int>("CleanupSettings:IntervalInSeconds", 6);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service Starting...");
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Service Stopping...");
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            foreach (var path in _directoryPaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
                {
                    try
                    {
                        Directory.CreateDirectory(path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogCritical(ex, $"Failed to create target directory: {path}. Service will not clean files.");
                    }
                }
            }


            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Service running...");

                int deletedCountThisCycle = 0;
                long deletedBytesThisCycle = 0;

                foreach (var currentPath in _directoryPaths)
                {
                    if (Directory.Exists(currentPath))
                    {
                        try
                        {

                            var files = Directory.EnumerateFiles(currentPath);
                            foreach (var file in files)
                            {
                                var extensions = Path.GetExtension(file);
                                if (_fileExtensions == null || _fileExtensions.Length == 0 || _fileExtensions.Contains(extensions, StringComparer.OrdinalIgnoreCase))
                                {
                                    if (_dryMode)
                                    {
                                        _logger.LogInformation($"[DRY MODE] Would delete file: {file}");
                                    }
                                    else
                                    {
                                        try
                                        {
                                            long fileSize = new FileInfo(file).Length;
                                            File.Delete(file);

                                            deletedBytesThisCycle += fileSize;
                                            deletedCountThisCycle++;

                                            _logger.LogInformation($"Deleted file: {file}, size: {FormatBytes(fileSize)}");
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, $"Failed to delete file: {file}");
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Failed to access directory: {currentPath}");

                        }
                    }
                }

                // If files were deleted, send an email report
                if (deletedCountThisCycle > 0)
                {
                    var totalFormattedSize = FormatBytes(deletedBytesThisCycle);
                    _logger.LogInformation($"Cycle complete. Deleted {deletedCountThisCycle} files, freeing {totalFormattedSize}.");
                    await SendEmailReportAsync(deletedCountThisCycle, totalFormattedSize, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(_IntervalInSeconds), stoppingToken);
            }
        }

        private bool CheckOfEmailSettings(string smtpServer, int smtpPort, string senderEmail, string senderPassword, string recipientEmail)
        {
            return  string.IsNullOrWhiteSpace(smtpServer) ||
                    string.IsNullOrWhiteSpace(senderEmail) ||
                    string.IsNullOrWhiteSpace(senderPassword) ||
                    string.IsNullOrWhiteSpace(recipientEmail);
        }
      
        private string GetHTMLEmailFormat(int fileCount, string totalSize, string serverName, string runTime)
        {
            return $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; color: #333; line-height: 1.6; padding: 20px; }}
                        .container {{ max-width: 600px; margin: 0 auto; background: #fff; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }}
                        h2 {{ color: #0056b3; border-bottom: 2px solid #eee; padding-bottom: 10px; }}
                        .stats-table {{ width: 100%; border-collapse: collapse; margin-top: 15px; }}
                        .stats-table th, .stats-table td {{ padding: 12px; border: 1px solid #ddd; text-align: left; }}
                        .stats-table th {{ background-color: #f8f9fa; font-weight: bold; width: 40%; }}
                        .footer {{ margin-top: 20px; font-size: 12px; color: #777; text-align: center; border-top: 1px solid #eee; padding-top: 10px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h2>Automated Cleanup Report</h2>
                        <p>Hello,</p>
                        <p>The background cleanup service has successfully completed a maintenance cycle. Below is the summary of the operation:</p>
                        
                        <table class='stats-table'>
                            <tr>
                                <th>Server / Machine</th>
                                <td>{serverName}</td>
                            </tr>
                            <tr>
                                <th>Time of Execution</th>
                                <td>{runTime}</td>
                            </tr>
                            <tr>
                                <th>Files Deleted</th>
                                <td><strong>{fileCount}</strong></td>
                            </tr>
                            <tr>
                                <th>Storage Freed</th>
                                <td><span style='color: #28a745; font-weight: bold;'>{totalSize}</span></td>
                            </tr>
                        </table>

                        <div class='footer'>
                            This is an automated message generated by the Configurable Cleanup Service. Please do not reply directly to this email.
                        </div>
                    </div>
                </body>
                </html>";
        }
        
        private async Task SendEmailReportAsync(int fileCount, string totalSize, CancellationToken cancellationToken = default)
        {
            try
            {
                var emailSettings = _configuration.GetRequiredSection("EmailSettings");

                var smtpServer = emailSettings["SmtpServer"];
                var smtpPort = emailSettings.GetValue<int?>("SmtpPort") ?? 587;
                var senderEmail = emailSettings["SenderEmail"];
                var senderPassword = (emailSettings["SenderPassword"] ?? string.Empty).Replace(" ", string.Empty);
                var recipientEmail = emailSettings["RecipientEmail"];

                if (CheckOfEmailSettings(smtpServer, smtpPort, senderEmail, senderPassword, recipientEmail))
                {
                    _logger.LogError("Email settings are missing or invalid.");
                    return;
                }

                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(senderEmail));
                message.To.Add(MailboxAddress.Parse(recipientEmail));
                message.Subject = $"Cleanup Service Report - {DateTime.Now:yyyy-MM-dd}";

                string serverName = Environment.MachineName;
                string runTime = DateTime.Now.ToString("f");

                string htmlBody = GetHTMLEmailFormat(fileCount, totalSize, serverName, runTime);

                message.Body = new TextPart("html")
                {
                    Text = htmlBody
                };

                using var client = new SmtpClient();
                await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls, cancellationToken);
                await client.AuthenticateAsync(senderEmail, senderPassword, cancellationToken);
                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                _logger.LogInformation("Email report sent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email report.");
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";

            double value = bytes;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value:0.##} {units[unit]}";
        }
    }
}



