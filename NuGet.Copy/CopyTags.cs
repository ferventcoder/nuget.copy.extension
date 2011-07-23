namespace NuGet.Copy
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.IO;
    using System.Linq;
    using Commands;
    using Common;

    [Command(typeof(CopyTagsResources), "copyTags", "Description", MinArgs = 1, MaxArgs = 5, UsageSummaryResourceName = "UsageSummary", UsageDescriptionResourceName = "UsageDescription")]
    public class CopyTags : Command
    {
        private readonly IPackageRepositoryFactory _repositoryFactory;
        private readonly IPackageSourceProvider _sourceProvider;
        private IList<string> _sources = new List<string>();

        [ImportingConstructor]
        public CopyTags(IPackageRepositoryFactory repositoryFactory, IPackageSourceProvider sourceProvider)
        {
            _repositoryFactory = repositoryFactory;
            _sourceProvider = sourceProvider;
        }

        [Option(typeof(CopyTagsResources), "SourceDescription", AltName = "src")]
        public IList<string> Source
        {
            get { return _sources; }
            set { _sources = value; }
        }

        [Option(typeof(CopyTagsResources), "DestinationDescription", AltName = "dest")]
        public string Destination { get; set; }

        [Option(typeof(CopyTagsResources), "ApiKeyDescription", AltName = "api")]
        public string ApiKey { get; set; }

        [Option(typeof(CopyTagsResources), "AllVersionsDescription",AltName="all")]
        public bool AllVersions { get; set; }

        public override void ExecuteCommand()
        {
            string tagId = base.Arguments[0];
            PrepareDestination();

            Console.WriteLine("Copying all packages with '{0}' tag from {1} to {2}.", tagId, "official nuget feed", Destination);

            var packages = GetPackages(tagId);

            Copy copy = new Copy(_repositoryFactory, _sourceProvider);
            copy.Console = this.Console;
            copy.ApiKey = ApiKey;
            copy.Source = Source;
            copy.Destination = Destination;

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
                    Console.WriteError("Had an error getting package '{0} - {1}': {2}",package.Id,package.Version.ToString(),ex.Message);
                }
            }
        }

        //private string PrepareUrl(string tagId)
        //{
        //    return string.Format(
        //            "http://packages.nuget.org/v1/FeedService.svc/Packages()?$filter=(Tags%20ne%20null)%20and%20substringof('%20{0}%20',tolower(Tags))&$top=500",
        //            tagId);
        //    //http://packages.nuget.org/v1/FeedService.svc/Packages()?$filter=(Tags%20ne%20null)%20and%20substringof('%20chocolatey%20',tolower(Tags))&$top=500

        //}

        private void PrepareDestination()
        {
            if (IsDirectory(Destination))
            {
                // destination is current directory
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

        private bool IsDirectory(string destination)
        {
            return string.IsNullOrEmpty(destination) || destination.Contains(@"\");
        }

        public IEnumerable<IPackage> GetPackages(string tagId)
        {
            IQueryable<IPackage> packages = GetRepository().GetPackages().OrderBy(p => p.Id).Where(p => p.Tags.Contains(tagId));
            //IPackageRepository packageRepository = GetRepository();
            //IQueryable<IPackage> packages = packageRepository.GetPackages().OrderBy(p => p.Id).Where(p => p.Tags.Contains(tagId));
            //return packages;
            if (AllVersions)
            {
                // Do not collapse versions
                return packages;
            }

            return packages.DistinctLast(PackageEqualityComparer.Id, PackageComparer.Version);
        }

        private IPackageRepository GetRepository()
        {
            AggregateRepository repository = AggregateRepositoryHelper.CreateAggregateRepositoryFromSources(_repositoryFactory, _sourceProvider, Source);
            repository.Logger = base.Console;
            return repository;
        }
    }
}