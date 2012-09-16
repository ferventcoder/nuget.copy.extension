namespace NuGet.Copy.Tests
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using Console = Common.Console;

    public class CommandLineRepositoryFactory : PackageRepositoryFactory
    {
        private const string UserAgentClient = "NuGet Command Line";

        public override IPackageRepository CreateRepository(string packageSource)
        {
            var repository = base.CreateRepository(packageSource);
            var httpClientEvents = repository as IHttpClientEvents;

            if (httpClientEvents != null)
            {
                httpClientEvents.SendingRequest += (sender, args) =>
                {
                    string userAgent = HttpUtility.CreateUserAgentString(UserAgentClient);
                    HttpUtility.SetUserAgent(args.Request, userAgent);
                };
            }

            return repository;
        }
    }
}
