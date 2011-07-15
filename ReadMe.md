# NuGet Copy Extension
This adds copy to the nuget.exe command line.  

`nuget.exe copy packageId [-Version version] [-Source sourceFeed] [-Destination destinationPathOrFeed]] [-ApiKey apiKey]`  
  
This will also copy all dependent packages.  

#Install
To use this package, please install [NuGet Extend](http://nuget.org/list/packages/addconsoleextension) first and then run  

`nuget.exe addExtension nuget.copy.extension`  

#Parameters
###PackageId
Name of package in source feed to copy to destination feed.  
###Version (optional)
The version of the package to copy.  
Defaults to the latest version available.  
###Source (optional)
Source (directory, share or remote url feed) the package comes from.  
Defaults to official nuget feed.  
###Destination (optional, highly recommended)
Location (directory, share or remote url feed) that the package will be copied to.  
Defaults to local directory.  
###ApiKey (optional)
The ApiKey if not already set (or just if you want to)  
Defaults to the one you have set or throws an error if you do not have one set and do not pass this parameter.  