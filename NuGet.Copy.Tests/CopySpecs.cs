namespace NuGet.Copy.Tests
{
    using System;
    using System.IO;
    using NUnit.Framework;
    using Should;
    using Console = Common.Console;

    public class CopySpecs
    {
        public abstract class CopySpecsBase : TinySpec
        {
            protected Copy command;
            protected string destDir1 = @".\dest1";
            protected string destDir2 = @".\dest2";
            protected string tagId = @"log4net";
            protected int expectedNumberOfPackages = 1;

            public override void Context()
            {
                RemoveAndCreateDirectory(destDir1);
                RemoveAndCreateDirectory(destDir2);

                var defaultPackageSource = new PackageSource(NuGetConstants.DefaultFeedUrl);
                var settings = Settings.LoadDefaultSettings();
                IPackageSourceProvider sourceProvider = new PackageSourceProvider(settings, new[] { defaultPackageSource });
                IPackageRepositoryFactory repositoryFactory = new CommandLineRepositoryFactory();

                command = new Copy(repositoryFactory, sourceProvider);
                command.Console = new Console();
            }

            protected void RemoveAndCreateDirectory(string directory)
            {
                if (Directory.Exists(directory)) { Directory.Delete(directory, true); }
                Directory.CreateDirectory(directory);
            }
        }

        [Category("integration")]
        public class when_copying_tags_from_the_default_package_source_to_one_local_destination : CopySpecsBase
        {
            public override void Context()
            {
                base.Context();

                command.Arguments.Add(tagId);
                command.Destination.Add(destDir1);
            }

            public override void Because()
            {
                command.Execute();
            }

            [Fact]
            public void should_run_successfully()
            {
            }

            [Fact]
            public void should_copy_the_packages_to_destDir1()
            {
                var dir = new DirectoryInfo(destDir1);
                dir.GetFiles().Length.ShouldEqual(expectedNumberOfPackages);
            }
        }

        [Category("integration")]
        public class when_copying_tags_from_the_default_package_source_to_two_local_destinations : CopySpecsBase
        {
            public override void Context()
            {
                base.Context();

                command.Arguments.Add(tagId);
                command.Destination.Add(destDir1);
                command.Destination.Add(destDir2);
            }

            public override void Because()
            {
                command.Execute();
            }

            [Fact]
            public void should_run_successfully()
            {
            }

            [Fact]
            public void should_copy_the_packages_to_destDir1()
            {
                var dir = new DirectoryInfo(destDir1);
                dir.GetFiles().Length.ShouldEqual(expectedNumberOfPackages);
            }

            [Fact]
            public void should_copy_the_packages_to_destDir2()
            {
                var dir = new DirectoryInfo(destDir2);
                dir.GetFiles().Length.ShouldEqual(expectedNumberOfPackages);
            }
        }

        [Category("integration")]
        public class when_copying_tags_with_dependent_packages_from_the_default_package_source_to_two_local_destinations : CopySpecsBase
        {

            public override void Context()
            {
                base.Context();

                command.Arguments.Add("common.logging.log4net");

                expectedNumberOfPackages = 3;

                command.Destination.Add(destDir1);
                command.Destination.Add(destDir2);
            }

            public override void Because()
            {
                command.Execute();
            }

            [Fact]
            public void should_run_successfully()
            {
            }

            [Fact]
            public void should_copy_the_packages_to_destDir1()
            {
                var dir = new DirectoryInfo(destDir1);
                dir.GetFiles().Length.ShouldEqual(expectedNumberOfPackages);
            }

            [Fact]
            public void should_copy_the_packages_to_destDir2()
            {
                var dir = new DirectoryInfo(destDir2);
                dir.GetFiles().Length.ShouldEqual(expectedNumberOfPackages);
            }
        }
    }
}