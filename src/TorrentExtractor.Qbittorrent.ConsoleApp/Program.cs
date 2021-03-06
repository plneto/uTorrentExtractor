﻿using System;
using System.Collections.Generic;
using System.Linq;
using CommandLine;
using MediatR;
using Serilog;
using TorrentExtractor.Core.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TorrentExtractor.Application.Commands;
using TorrentExtractor.Qbittorrent.ConsoleApp.Helpers;
using TorrentExtractor.Qbittorrent.ConsoleApp.Models;
using TorrentExtractor.Core.Settings;
using TorrentExtractor.Domain.Services;

namespace TorrentExtractor.Qbittorrent.ConsoleApp
{
    internal class Program
    {
        public static IConfigurationRoot Configuration { get; set; }

        public static IServiceProvider ServiceProvider { get; set; }

        private static void Main(string[] args)
        {
            Init();

            Log.Debug("Program Started");

            var mediator = ServiceProvider.GetService<IMediator>();

            try
            {
                var qbittorrentOptions = GetQbittorrentOptions(args);

                var command = new TorrentProcessDownload.Command(
                    qbittorrentOptions.Category,
                    qbittorrentOptions.ContentPath,
                    qbittorrentOptions.TorrentName);

                mediator.Send(command);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error Extracting Files");
            }

            Log.Debug("Program Finished!");
        }

        private static void Init()
        {
            var configBuilder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json");

            Configuration = configBuilder.Build();

            var logFilePath = Configuration["LoggingSettings:LogFilePath"];

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.RollingFile(logFilePath)
                .WriteTo.ColoredConsole()
                .CreateLogger();

            Log.Information("Logger online");

            var torrentSettings = new TorrentSettings();
            var emailSettings = new EmailSettings();
            var loggingSettings = new LoggingSettings();

            Configuration.GetSection(nameof(TorrentSettings)).Bind(torrentSettings);
            Configuration.GetSection(nameof(EmailSettings)).Bind(emailSettings);
            Configuration.GetSection(nameof(LoggingSettings)).Bind(loggingSettings);

            IServiceCollection services = new ServiceCollection();

            services.AddSingleton(ctx => torrentSettings);
            services.AddSingleton(ctx => emailSettings);
            services.AddSingleton(ctx => loggingSettings);

            services.AddMediatR(AppDomain.CurrentDomain.GetAssemblies());

            services.AddTransient<ITorrentDomainService, TorrentDomainService>();
            services.AddTransient<IFileHandler, FileHandler>();
            services.AddTransient<INotificationService, NotificationService>();

            ServiceProvider = services.BuildServiceProvider();
        }

        public static QbittorrentOptions GetQbittorrentOptions(string[] args)
        {
            args = ArgsHelper.RemoveEmptyArgs(args);

            if (args.Length > 0)
                Log.Debug("Args: {0}", string.Join(" ", args));
            else
                Log.Information("No args provided");

            var options = Parser.Default.ParseArguments<QbittorrentOptions>(args);

            if (options.Tag == ParserResultType.NotParsed)
            {
                Log.Error("Error parsing arguments");

                var errors = new List<Error>();
                options.WithNotParsed(x => errors = x.ToList());

                foreach (var error in errors)
                {
                    Log.Error(error.ToString());
                }

                throw new Exception("Failed to parse arguments");
            }

            var qbittorrentOptions = new QbittorrentOptions();

            options.WithParsed(x => qbittorrentOptions = x);

            return qbittorrentOptions;
        }
    }
}