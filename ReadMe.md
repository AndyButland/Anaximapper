# Anaximapper

## Background

Anaximapper is an Umbraco V9 port of [Umbraco Mapper](https://github.com/AndyButland/UmbracoMapper), which was originally developed to support a more pure MVC approach to building Umbraco applications.  Since it's development a number of other similar solutions have been released, including of course "models builder", now part of Umbraco core.

If your preferred approach to MVC with Umbraco though is to use route hijacking and construct page specific view models, rather than relying on the one-to-one mapping of document types to content models available with models builder, you may find Anaximapper a useful addition.  Via convention and configuration, you can have your view models automatically populated from Umbraco data, without having to manually map each property yourself.

## Why Anaximapper?

Well, I needed a new name... and this is what I came up with!  Since the release of the original Umbraco Mapper, Umbraco themselves have introduced a class called `UmbracoMapper` in the core code base, that contributors and implementors may well come across.  So, to avoid confusion, thought it was better to have a new name.

[Anaximander](https://en.wikipedia.org/wiki/Anaximander) was a philospher from ancient Greece, where one of his contributions was as a cartographer, creating one of first maps of the world as it was known at the time.  So as an "early mapper", figured I'll adopt him for this package.

## Using Anaximapper

As things stand Anaximapper is a functionally equivalent port of the original Umbraco Mapper, so I'll defer to the [read me file there](https://github.com/AndyButland/UmbracoMapper) for project documentation.

## Version History

- 1.0.0-beta-1 - Beta release of port to Umbraco V9.
