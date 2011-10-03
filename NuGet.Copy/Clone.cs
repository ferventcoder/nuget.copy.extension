namespace NuGet.Clone
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.IO;
    using Commands;
    using Common;
    using NuGet.Copy;
    using System.Linq;

    [Command(typeof(CloneResources), "clone", "Description", MinArgs = 0, MaxArgs = 5, UsageSummaryResourceName = "UsageSummary", UsageDescriptionResourceName = "UsageDescription")]
    public class Clone : Command
    {
        private readonly IPackageRepositoryFactory _repositoryFactory;
        private readonly IPackageSourceProvider _sourceProvider;
        private IList<string> _sources = new List<string>();
        private IList<string> _destinations = new List<string>();

        [ImportingConstructor]
        public Clone(IPackageRepositoryFactory repositoryFactory, IPackageSourceProvider sourceProvider)
        {
            _repositoryFactory = repositoryFactory;
            _sourceProvider = sourceProvider;
        }

        [Option(typeof(CloneResources), "SourceDescription", AltName = "src")]
        public IList<string> Source
        {
            get { return _sources; }
        }

        [Option(typeof(CloneResources), "DestinationDescription", AltName = "dest")]
        public IList<string> Destination
        {
            get { return _destinations; }
        }

        [Option(typeof(CloneResources), "ApiKeyDescription", AltName = "api")]
        public string ApiKey { get; set; }

        public override void ExecuteCommand()
        {

            string packageId = base.Arguments.Count > 0 ? base.Arguments[0] : string.Empty;
            List<string> packageList = new List<string>();
            //Either use just one package id
            if (!string.IsNullOrEmpty(packageId))
            {
                packageList.Add(packageId);
            }
            else
            {
                //or get the full list from the source, and go from there....
                packageList.AddRange(GetPackageList(false, string.Empty).Select(p => p.Id));
            }

            //Grab each package, get the full list of versions, and then call a Copy on each.
            //TODO Copy is currently using the default InstallCommand under the covers, which means this is a bit messy on the dependencies (ie it gets them all)
            foreach (var packageName in packageList)
            {
                var packages = GetPackageList(true, packageId);
                Console.WriteLine(string.Format("Found {0} versions of {1}", packages.Count(), packageId));
                foreach (var package in packages)
                {
                    Copy copyCommand = new Copy(_repositoryFactory, _sourceProvider)
                    {
                        ApiKey = ApiKey,
                        Destination = Destination,
                        Source = Source,
                        Version = package.Version.ToString(),
                        Console = this.Console,
                        Recursive = false
                    };
                    copyCommand.Arguments.Add(package.Id);
                    copyCommand.Execute();
                }
            }
        }

        public IEnumerable<IPackage> GetPackageList(bool allVersions, string id)
        {
            bool singular = string.IsNullOrEmpty(id) ? false : true;
            ListCommand listCommand = new ListCommand(_repositoryFactory, _sourceProvider)
            {
                AllVersions = allVersions,
                Console = this.Console,
            };
            if (singular)
                listCommand.Arguments.Add(id);
            var packages = listCommand.GetPackages();

            //listcommand doesnt return just the matching packages, so filter here...
            if (singular)
                return packages.Where(p => p.Id.ToLowerInvariant() == id.ToLowerInvariant());
            else
                return packages;
        }
    }
}