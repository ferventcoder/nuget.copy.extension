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
        private readonly string _workDirectory;

        [ImportingConstructor]
        public Copy(IPackageRepositoryFactory repositoryFactory, IPackageSourceProvider sourceProvider)
        {
            _repositoryFactory = repositoryFactory;
            _sourceProvider = sourceProvider;
            _workDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NugetCopyExtensionWork");
        }

        [Option(typeof(CopyResources), "SourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(CopyResources), "DestinationDescription", AltName = "dest")]
        public string Destination { get; set; }

        [Option(typeof(CopyResources), "VersionDescription")]
        public string Version { get; set; }

        [Option(typeof(CopyResources), "ApiKeyDescription", AltName = "api")]
        public string ApiKey { get; set; }

        public override void ExecuteCommand()
        {
            try
            {
                string packageId = base.Arguments[0];

                PrepareDestination();
                PrepareApiKey();

                Console.WriteLine("Copying {0} from {1} to {2}.", string.IsNullOrEmpty(Version) ? packageId : packageId + " " + Version, string.IsNullOrEmpty(Source) ? "any source" : Source, Destination);

                CreateWorkDirectoryIfNotExists(_workDirectory);
                InstallPackageLocally(packageId, _workDirectory);
                PushToDestination(_workDirectory);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                CleanUpWorkDirectory(_workDirectory);
            }
        }

        private void PrepareDestination()
        {
            if (IsDirectory(Destination))
            {
                //destination is current directory
                if (string.IsNullOrEmpty(Destination) || Destination == ".")
                {
                    Destination = Directory.GetCurrentDirectory();
                }

                // not a UNC Path
                if (!Destination.StartsWith(@"\\"))
                {
                    Destination = Path.GetFullPath(Destination);
                }
            }
        }

        private void PrepareApiKey()
        {
            if (!IsDirectory(Destination))
            {
                if (string.IsNullOrEmpty(ApiKey))
                {
                    ApiKey = GetApiKey(_sourceProvider, Settings.UserSettings, Destination, true);
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
            if (!string.IsNullOrEmpty(Version))
            {
                install.Version = Version;
            }

            install.ExecuteCommand();
        }

        private void PushToDestination(string workDirectory)
        {
            IList<string> PackagePaths = GetPackages(workDirectory);
            foreach (var packagePath in PackagePaths)
            {
                if (IsDirectory(Destination))
                {
                    PushToDestinationDirectory(packagePath);
                }
                else
                {
                    PushToDestinationRemote(packagePath);
                }
            }
        }

        private IList<string> GetPackages(string workDirectory)
        {
            return Directory.GetFiles(workDirectory, "*.nupkg", SearchOption.AllDirectories);
        }

        private bool IsDirectory(string destination)
        {
            return string.IsNullOrEmpty(destination) || destination.Contains(@"\");
        }

        private void PushToDestinationDirectory(string packagePath)
        {
            File.Copy(Path.GetFullPath(packagePath), Path.Combine(Destination, Path.GetFileName(packagePath)), true);
        }

        private void PushToDestinationRemote(string packagePath)
        {
            try
            {
                //PushCommand push = new PushCommand(_sourceProvider);
                //push.Arguments.Add(Path.GetFullPath(packagePath));
                //push.Source = _sourceProvider.ResolveSource(Destination);
                //push.ExecuteCommand();

                PushPackage(Path.GetFullPath(packagePath), Destination, ApiKey);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Copy encountered an issue. Perhaps the package already exists? {0}{1}", Environment.NewLine, ex.ToString());
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