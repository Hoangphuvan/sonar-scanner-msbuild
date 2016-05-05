﻿//-----------------------------------------------------------------------
// <copyright file="AnalysisConfigGenerator.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------

using SonarQube.Common;
using SonarQube.TeamBuild.Integration;
using System;
using System.Collections.Generic;

namespace SonarQube.TeamBuild.PreProcessor
{
    public static class AnalysisConfigGenerator
    {
        /// <summary>
        /// Combines the various configuration options into the AnalysisConfig file
        /// used by the build and post-processor. Saves the file and returns the config instance.
        /// </summary>
        /// <param name="args">Processed command line arguments supplied the user</param>
        /// <param name="buildSettings">Build environment settings</param>
        /// <param name="serverProperties">Analysis properties downloaded from the SonarQube server</param>
        /// <param name="analyzerSettings">Specifies the Roslyn analyzers to use</param>
        /// <param name="programmaticProperties">Any additional programmatically set analysis properties. Any user-specified values will take priority</param>
        public static AnalysisConfig GenerateFile(ProcessedArgs args,
            TeamBuildSettings buildSettings,
            IDictionary<string, string> serverProperties,
            AnalyzerSettings analyzerSettings,
            AnalysisProperties programmaticProperties,
            ILogger logger)
        {
            if (args == null)
            {
                throw new ArgumentNullException("args");
            }
            if (buildSettings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (serverProperties == null)
            {
                throw new ArgumentNullException("serverProperties");
            }
            if (analyzerSettings == null)
            {
                throw new ArgumentNullException("analyzerSettings");
            }
            if (programmaticProperties == null)
            {
                throw new ArgumentNullException("programmaticProperties");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            AnalysisConfig config = new AnalysisConfig();
            config.SonarProjectKey = args.ProjectKey;
            config.SonarProjectName = args.ProjectName;
            config.SonarProjectVersion = args.ProjectVersion;
            config.SonarQubeHostUrl = args.GetSetting(SonarProperties.HostUrl);

            config.SetBuildUri(buildSettings.BuildUri);
            config.SetTfsUri(buildSettings.TfsUri);

            config.SonarConfigDir = buildSettings.SonarConfigDirectory;
            config.SonarOutputDir = buildSettings.SonarOutputDirectory;
            config.SonarBinDir = buildSettings.SonarBinDirectory;
            config.SonarRunnerWorkingDirectory = buildSettings.SonarRunnerWorkingDirectory;
            config.SourcesDirectory = buildSettings.SourcesDirectory;

            // Add the server properties to the config
            config.ServerSettings = new AnalysisProperties();

            foreach (var property in serverProperties)
            {
                if (!IsSecuredServerProperty(property.Key))
                {
                    AddSetting(config.ServerSettings, property.Key, property.Value);
                }
            }

            // Add command line arguments and programmatic properties.
            // Command line arguments take precedence
            AggregatePropertiesProvider aggProvider = new AggregatePropertiesProvider(args.LocalProperties, new ListPropertiesProvider(programmaticProperties));

            config.LocalSettings = new AnalysisProperties();
            foreach (var property in aggProvider.GetAllProperties())
            {
                AddSetting(config.LocalSettings, property.Id, property.Value);
            }

            // Set the pointer to the properties file
            if (args.PropertiesFileName != null)
            {
                config.SetSettingsFilePath(args.PropertiesFileName);
            }

            config.AnalyzerSettings = analyzerSettings;

            config.Save(buildSettings.AnalysisConfigFilePath);

            return config;
        }

        private static void AddSetting(AnalysisProperties properties, string id, string value)
        {
            Property property = new Property() { Id = id, Value = value };

            // Ensure it isn't possible to write sensitive data to the config file
            if (!property.ContainsSensitiveData())
            {
                properties.Add(new Property() { Id = id, Value = value });
            }
        }

        private static bool IsSecuredServerProperty(string s)
        {
            return s.EndsWith(".secured", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}