# Helper library that aims to provide unified capabilites for embedded hosts applications.

It was started due to limitations of .NET Core delegates, where basically only privimitve types are marshalled when using CreateDelegate method, while Mono provides just what you might expect with little to no hassle.
So this library tries to provide same API and capabilites to both runtimes.

Targets .NET Standard 2.0

Build with .NET Core 2.1 or higher. Don't forget to copy the contents from this path next to your program resources.

```dotnet publish -o <out_path> -c Release```