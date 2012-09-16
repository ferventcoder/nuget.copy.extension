namespace NuGet.Copy
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.IO;
    using System.Linq;
    using Commands;
    using Common;
    using Console = Common.Console;

    [Command(typeof(CopySearchResources), "copySearch", "Description", MinArgs = 1, MaxArgs = 5, UsageSummaryResourceName = "UsageSummary", UsageDescriptionResourceName = "UsageDescription")]
    public class CopySearch : Command
    {
        private readonly IPackageRepositoryFactory _repositoryFactory;
        private readonly IPackageSourceProvider _sourceProvider;
        private IList<string> _sources = new List<string>();
        private IList<string> _destinations = new List<string>();

        [ImportingConstructor]
        public CopySearch(IPackageRepositoryFactory repositoryFactory, IPackageSourceProvider sourceProvider)
        {
            _repositoryFactory = repositoryFactory;
            _sourceProvider = sourceProvider;
        }

        [Option(typeof(CopySearchResources), "SourceDescription", AltName = "src")]
        public IList<string> Source
        {
            get { return _sources; }
            set { _sources = value; }
        }

        [Option(typeof(CopySearchResources), "DestinationDescription", AltName = "dest")]
        public IList<string> Destination
        {
            get { return _destinations; }
            set { _destinations = value; }
        }

        //[Option(typeof(CopySearchResources), "DestinationDescription", AltName = "dest")]
        //public string Destination { get; set; }

        [Option(typeof(CopySearchResources), "ApiKeyDescription", AltName = "api")]
        public string ApiKey { get; set; }

        [Option(typeof(CopySearchResources), "AllVersionsDescription", AltName = "all")]
        public bool AllVersions { get; set; }

        public override void ExecuteCommand()
        {
            string searchId = base.Arguments[0];
            PrepareSources();
            PrepareDestinations();

            Console.WriteLine("Copying all packages with '{0}' from {1} to {2}.", searchId, Source.Count == 0 ? "any source" : string.Join(";", Source), string.Join(";", Destination));

            IEnumerable<IPackage> packages = new List<IPackage>();
            try
            {
                packages = GetPackages(searchId);
                Console.WriteLine("Retrieved {0} packages, not counting dependencies for copying from one or more sources to one or more destinations.", packages.Count());
            }
            catch (Exception)
            {
                Console.WriteError("Oopsy!");
                throw;
            }

            Copy copy = new Copy(_repositoryFactory, _sourceProvider);
            copy.Console = this.Console;
            copy.ApiKey = ApiKey;
            foreach (string src in Source)
            {
                copy.Source.Add(src);
            }
            foreach (string dest in Destination)
            {
                copy.Destination.Add(dest);
            }

            IList<string> report = new List<string>();

            foreach (IPackage package in packages)
            {
                copy.Arguments.Clear();
                copy.Arguments.Add(package.Id);
                copy.Version = package.Version.ToString();
                try
                {
                    copy.Execute();
                }
                catch (Exception ex)
                {
                    string message = string.Format("Had an error getting package '{0} - {1}': {2}", package.Id, package.Version.ToString(), ex.Message);
                    Console.WriteError(message);
                    report.Add(message);
                }
            }

            PrintReport(searchId, report);
        }

        //private string PrepareUrl(string searchFilter)
        //{
        //    return string.Format(
        //            "http://packages.nuget.org/v1/FeedService.svc/Packages()?$filter=(Tags%20ne%20null)%20and%20substringof('%20{0}%20',tolower(Tags))&$top=500",
        //            searchFilter);
        //    //http://packages.nuget.org/v1/FeedService.svc/Packages()?$filter=(Tags%20ne%20null)%20and%20substringof('%20chocolatey%20',tolower(Tags))&$top=500

        //}

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

        private bool IsDirectory(string destination)
        {
            return string.IsNullOrWhiteSpace(destination) || destination.Contains(@"\") || destination == ".";
        }

        public IEnumerable<IPackage> GetPackages(string searchFilter)
        {
            var packages = GetRepository().GetPackages()
                                          .OrderBy(p => p.Id)
                                          .Take(20).ToList();
            var filteredPackages = packages.AsQueryable()
                                          .Find(searchFilter).ToList();

            if (AllVersions)
            {
                // Do not collapse versions
                return filteredPackages;
            }

            var filteredUniquePackages = filteredPackages;
            return filteredUniquePackages.Distinct(PackageEqualityComparer.IdAndVersion);
        }

        private IPackageRepository GetRepository()
        {
            AggregateRepository repository = AggregateRepositoryHelper.CreateAggregateRepositoryFromSources(_repositoryFactory, _sourceProvider, Source);
            repository.Logger = base.Console;
            return repository;
        }

        private void PrintReport(string searchFilter, IList<string> report)
        {
            if (report.Count != 0)
            {
                Console.WriteWarning("Finished copying all packages with searchFilter '{0}' except where the following errors occurred:", searchFilter);
                foreach (string line in report)
                {
                    Console.WriteWarning("  " + line);
                }
            }
            else
            {
                Console.WriteLine("Finished copying all packages with searchFilter '{0}' successfully.", searchFilter);
            }
        }
    }
}