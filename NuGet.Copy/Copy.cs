namespace NuGet.Copy
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.IO;
    using Commands;
    using Common;

    [Command(typeof(CopyResources), "copy", "Description", MinArgs = 1, MaxArgs = 5, UsageSummaryResourceName = "UsageSummary", UsageDescriptionResourceName = "UsageDescription")]
    public class Copy : Command
    {
        private readonly IPackageRepositoryFactory _repositoryFactory;
        private readonly IPackageSourceProvider _sourceProvider;
        private IList<string> _sources = new List<string>();
        private IList<string> _destinations = new List<string>();
        private readonly string _workDirectory;

        [ImportingConstructor]
        public Copy(IPackageRepositoryFactory repositoryFactory, IPackageSourceProvider sourceProvider)
        {
            _repositoryFactory = repositoryFactory;
            _sourceProvider = sourceProvider;
            _workDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NugetCopyExtensionWork");
        }

        [Option(typeof(CopySearchResources), "SourceDescription", AltName = "src")]
        public IList<string> Source
        {
            get { return _sources; }
        }

        //[Option(typeof(CopyResources), "SourceDescription", AltName = "src")]
        //public string Source { get; set; }

        [Option(typeof(CopyResources), "DestinationDescription", AltName = "dest")]
        public IList<string> Destination
        {
            get { return _destinations; }
        }

        //[Option(typeof(CopyResources), "DestinationDescription", AltName = "dest")]
        //public string Destination { get; set; }

        [Option(typeof(CopyResources), "VersionDescription")]
        public string Version { get; set; }

        [Option(typeof(CopyResources), "ApiKeyDescription", AltName = "api")]
        public string ApiKey { get; set; }

        public override void ExecuteCommand()
        {
            CleanUpWorkDirectory(_workDirectory);
            string packageId = base.Arguments[0];
            PrepareSources();
            PrepareDestinations();

            PreventApiKeyBeingSpecifiedWhenMultipleRemoteSources();

            Console.WriteLine("Copying {0} and all of its dependent packages from {1} to {2}.", string.IsNullOrEmpty(Version) ? packageId : packageId + " " + Version,
                                              Source.Count == 0 ? "any source" : string.Join(";", Source), string.Join(";", Destination));

            CreateWorkDirectoryIfNotExists(_workDirectory);
            InstallPackageLocally(packageId, _workDirectory);

            foreach (string dest in Destination)
            {
                PrepareApiKey(dest);
                PushToDestination(_workDirectory,dest);
            }
        }

        private void PrepareSources()
        {
            if (Source.Count == 0)
            {
                Source.Add(".");
            }

            for (int i = 0; i < Source.Count; i++)
            {
                if (IsDirectory(Source[i]))
                {
                    //destination is current directory
                    if (string.IsNullOrWhiteSpace(Source[i]) || Source[i] == ".")
                    {
                        Source[i] = Directory.GetCurrentDirectory();
                    }

                    // not a UNC Path
                    if (!Source[i].StartsWith(@"\\"))
                    {
                        Source[i] = Path.GetFullPath(Source[i]);
                    }
                }
            }
        }      
        
        private void PrepareDestinations()
        {
            if (Destination.Count == 0)
            {
                Destination.Add(".");
            }

            for (int i = 0; i < Destination.Count; i++)
            {
                if (IsDirectory(Destination[i]))
                {
                    //destination is current directory
                    if (string.IsNullOrWhiteSpace(Destination[i]) || Destination[i] == ".")
                    {
                        Destination[i] = Directory.GetCurrentDirectory();
                    }

                    // not a UNC Path
                    if (!Destination[i].StartsWith(@"\\"))
                    {
                        Destination[i] = Path.GetFullPath(Destination[i]);
                    }
                }
            }
        }

        private void PreventApiKeyBeingSpecifiedWhenMultipleRemoteSources()
        {
            var remoteCount = 0;
            foreach (string dest in Destination)
            {
                if (!IsDirectory(dest))
                {
                    remoteCount += 1;
                }
            }
            if (!string.IsNullOrWhiteSpace(ApiKey) && remoteCount > 1)
            {
                throw new ApplicationException("ApiKey cannot be set if you specify multiple remote destinations. Please consider using nuget 'setApiKey' command and then running this command without the ApiKey parameter set.");
            }
        }

        private void PrepareApiKey(string destination)
        {
            if (!IsDirectory(destination))
            {
                if (string.IsNullOrEmpty(ApiKey))
                {
                    ApiKey = GetApiKey(_sourceProvider, Settings.UserSettings, destination, true);
                }
            }
        }

        private void CreateWorkDirectoryIfNotExists(string workDirectory)
        {
            if (!Directory.Exists(workDirectory))
            {
                Directory.CreateDirectory(workDirectory);
            }
        }

        private void InstallPackageLocally(string packageId, string workDirectory)
        {
            InstallCommand install = new InstallCommand(_repositoryFactory, _sourceProvider);
            install.Arguments.Add(packageId);
            install.OutputDirectory = workDirectory;
            install.Console = this.Console;
            foreach (var source in Source)
            {
                install.Source.Add(source);
            }
            if (!string.IsNullOrEmpty(Version))
            {
                install.Version = Version;
            }

            install.ExecuteCommand();
        }

        private void PushToDestination(string workDirectory, string destination)
        {
            IList<string> PackagePaths = GetPackages(workDirectory);
            foreach (var packagePath in PackagePaths)
            {
                if (IsDirectory(destination))
                {
                    PushToDestinationDirectory(packagePath, destination);
                }
                else
                {
                    PushToDestinationRemote(packagePath, destination);
                }
            }
        }

        private IList<string> GetPackages(string workDirectory)
        {
            return Directory.GetFiles(workDirectory, "*.nupkg", SearchOption.AllDirectories);
        }

        private bool IsDirectory(string destination)
        {
            return string.IsNullOrWhiteSpace(destination) || destination.Contains(@"\") || destination == ".";
        }

        private void PushToDestinationDirectory(string packagePath, string destination)
        {
            File.Copy(Path.GetFullPath(packagePath), Path.Combine(destination, Path.GetFileName(packagePath)), true);
            Console.WriteLine("Completed copying '{0}' to '{1}'", Path.GetFileName(packagePath), destination);
        }

        private void PushToDestinationRemote(string packagePath, string destination)
        {
            try
            {
                //PushCommand push = new PushCommand(_sourceProvider);
                //push.Arguments.Add(Path.GetFullPath(packagePath));
                //push.Source = _sourceProvider.ResolveSource(Destination);
                //push.Console = this.Console;
                //push.ExecuteCommand();

                PushPackage(Path.GetFullPath(packagePath), destination, ApiKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Copy encountered an issue. Perhaps the package already exists? {0}{1}", Environment.NewLine, ex);
            }
        }

        #region Push Command Not Working

        private static readonly string ApiKeysSectionName = "apikeys";

        private static string GetApiKey(IPackageSourceProvider sourceProvider, ISettings settings, string source, bool throwIfNotFound)
        {
            string apiKey = settings.GetDecryptedValue(ApiKeysSectionName, source);
            if (string.IsNullOrEmpty(apiKey) && throwIfNotFound)
            {
                throw new CommandLineException(
                    "No API Key was provided and no API Key could be found for {0}. To save an API Key for a source use the 'setApiKey' command.",
                    new object[] { sourceProvider.GetDisplayName(source) });
            }
            return apiKey;
        }

        private void PushPackage(string packagePath, string source, string apiKey)
        {
            var gallery = new GalleryServer(source);

            // Push the package to the server
            var package = new ZipPackage(packagePath);

            bool complete = false;
            gallery.ProgressAvailable += (sender, e) =>
            {
                Console.Write("\r" + "Pushing: {0}", e.PercentComplete);

                if (e.PercentComplete == 100)
                {
                    Console.WriteLine();
                    complete = true;
                }
            };

            Console.WriteLine("Pushing {0} to {1}", package.GetFullName(), _sourceProvider.GetDisplayName(source));

            try
            {
                using (Stream stream = package.GetStream())
                {
                    gallery.CreatePackage(apiKey, stream);
                }
            }
            catch
            {
                if (!complete)
                {
                    Console.WriteLine();
                }
                throw;
            }

            // Publish the package on the server

            var cmd = new PublishCommand(_sourceProvider);
            cmd.Console = Console;
            cmd.Source = source;
            cmd.Arguments = new List<string>
                                {
                                    package.Id,
                                    package.Version.ToString(),
                                    apiKey
                                };
            cmd.Execute();
        }

        #endregion

        private void CleanUpWorkDirectory(string workDirectory)
        {
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, true);
            }
        }
    }
}