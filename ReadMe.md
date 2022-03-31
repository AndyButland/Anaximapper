# Anaximapper

## Background

Anaximapper is an Umbraco V9 port of [Umbraco Mapper](https://github.com/AndyButland/UmbracoMapper), which was originally developed to support a more pure MVC approach to building Umbraco applications.  Since it's development a number of other similar solutions have been released, including of course "models builder", now part of Umbraco core.

If your preferred approach to MVC with Umbraco though is to use route hijacking and construct page specific view models, rather than relying on the one-to-one mapping of document types to content models available with models builder, you may find Anaximapper a useful addition.  Via convention and configuration, you can have your view models automatically populated from Umbraco data, without having to manually map each property yourself.

## Why Anaximapper?

Well, I needed a new name... and this is what I came up with!  Since the release of the original Umbraco Mapper, Umbraco themselves have introduced a class called `UmbracoMapper` in the core code base, that contributors and implementors may well come across.  So, to avoid confusion, thought it was better to have a new name.

[Anaximander](https://en.wikipedia.org/wiki/Anaximander) was a philospher from ancient Greece, where one of his contributions was as a cartographer, creating one of first maps of the world as it was known at the time.  So as an "early mapper", figured I'll adopt him for this package.

## Installing Anaximapper

With an Umbraco 9 project, add the NuGet package via `dotnet add package Anaximapper --version 1.0.0-beta-1`.

Add the `AddAnanximapper()` extension method call to following to your `StartUp.cs` file in the `ConfigureServices()` method, e.g.:

```c#
    services.AddUmbraco(_env, _config)
        .AddBackOffice()
        .AddWebsite()
        .AddComposers()
        .AddAnaximapper()
        .Build();
```

Make sure Umbraco's models builder functionality is switched off, i.e. in `appSettings.json` ensure you have:

```json
  "Umbraco": {
    "CMS": {
      "ModelsBuilder": {
        "ModelsMode": "Nothing"
      }
    }
```

Build and run.

## Using Anaximapper

As things stand Anaximapper is a functionally equivalent port of the original Umbraco Mapper, so I'll defer to the [read me file there](https://github.com/AndyButland/UmbracoMapper) for project documentation, and just note here specific changes for this version.

Various examples of usage can be found in the test cases in this repository, either in the unit tests or in the test web application.

### Changes For Umbraco V9 Support

#### Mapping

The main mapping class, defining the `Map()` methods, is available by injecting `IPublishedContentMapper` into your controllers.

#### Working with IMapFromAttribute

The signature of `IMapFromAttribute` has changed, so allow flexibility in accessing services registered with the IoC container.  It now looks like this:

```c#
void SetPropertyValue<T>(object fromObject, PropertyInfo property, T model, MappingContext context);
```

The `context` attribute provides access to the mapper as was available before, but now also available is the `HttpContext`, which will allow access to registered services via e.g. : `context.HttpContext.RequestServices.GetRequiredService<IImageUrlGenerator>();`.


## Version History

- 1.0.0-beta-1 - Beta release of port to Umbraco V9.
- 1.0.0 - Full release of port to Umbraco V9.
- 1.0.1 - Added tag for Umbraco-Marketplace
